#pragma warning disable CS0162 // Unreachable code detected

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public partial class FileChunkManagerSystem : SystemBase
{
    const bool EnableLogging = false;
    public static string? BasePath => Application.streamingAssetsPath;

    [NotNull] Dictionary<FileId, RemoteFile>? RemoteFiles = new();
    [NotNull] List<FileRequest>? Requests = new();
    double NextCheckAt;
    Entity DatabaseEntity;

    public static FileChunkManagerSystem GetInstance(World world)
        => world.GetExistingSystemManaged<FileChunkManagerSystem>();

    protected override void OnUpdate()
    {
        if (NextCheckAt > SystemAPI.Time.ElapsedTime) return;
        NextCheckAt = SystemAPI.Time.ElapsedTime + .2d;

        if ((Requests.Count > 0 || DatabaseEntity == Entity.Null) &&
            !SystemAPI.TryGetSingletonEntity<BufferedFiles>(out DatabaseEntity))
        {
            Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Buffered files singleton not found");
            return;
        }

        if (Requests.Count == 0) return;

        EntityCommandBuffer commandBuffer = default;

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.EntityManager.GetBuffer<BufferedReceivingFile>(DatabaseEntity);
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks = World.EntityManager.GetBuffer<BufferedReceivingFileChunk>(DatabaseEntity);

        for (int i = Requests.Count - 1; i >= 0; i--)
        {
            HandleRequest(
                ref commandBuffer,
                fileHeaders,
                fileChunks,
                Requests[i],
                out bool shouldDelete,
                out int headerIndex
            );

            if (shouldDelete)
            {
                Requests.RemoveAt(i);
                CleanupRequest(
                    headerIndex,
                    fileHeaders,
                    fileChunks
                );
            }
        }
    }

    static void CleanupRequest(
        int headerIndex,
        DynamicBuffer<BufferedReceivingFile> fileHeaders,
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks)
    {
        BufferedReceivingFile header = fileHeaders[headerIndex];
        fileHeaders.RemoveAt(headerIndex);
        for (int i = fileChunks.Length - 1; i >= 0; i--)
        {
            if (fileChunks[i].Source != header.Source) continue;
            if (fileChunks[i].TransactionId != header.TransactionId) continue;
            fileChunks.RemoveAt(i);
        }
    }

    unsafe void HandleRequest(
        ref EntityCommandBuffer commandBuffer,
        DynamicBuffer<BufferedReceivingFile> fileHeaders,
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks,
        in FileRequest request,
        out bool shouldDelete,
        out int headerIndex)
    {
        shouldDelete = false;
        headerIndex = -1;

        for (int i = fileHeaders.Length - 1; i >= 0; i--)
        {
            BufferedReceivingFile header = fileHeaders[i];
            if (header.FileName != request.File.Name) continue;
            if (header.Source != request.File.Source) continue;

            headerIndex = i;

            if (header.Kind == FileResponseStatus.NotFound)
            {
                shouldDelete = true;

                Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Remote file \"{request.File.Name}\" not found");
                RemoteFiles.Remove(request.File);
                request.Task.SetException(new FileNotFoundException("Remote file not found", request.File.Name.ToString()));
                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                CloseRemoteFile(commandBuffer, request.File);

                return;
            }

            int totalLength = FileChunkManagerSystem.GetChunkLength(header.TotalLength);

            FileChunk[] chunks = new FileChunk[totalLength];
            bool[] received = new bool[totalLength];

            for (int j = 0; j < fileChunks.Length; j++)
            {
                if (fileChunks[j].TransactionId != header.TransactionId) continue;
                chunks[fileChunks[j].ChunkIndex] = fileChunks[j].Data;
                received[fileChunks[j].ChunkIndex] = true;
            }

            int receivedLength = received.Count(v => v);

            request.Progress?.Report((receivedLength, totalLength));

            if (receivedLength < totalLength)
            { return; }

            byte[] data = new byte[header.TotalLength];
            for (int j = 0; j < chunks.Length; j++)
            {
                int chunkSize = FileChunkManagerSystem.GetChunkSize(header.TotalLength, j);
                Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[j]), chunkSize);
                chunk.CopyTo(data.AsSpan(j * FileChunkResponseRpc.ChunkSize));
            }

            RemoteFile remoteFile = new(
                header.Kind,
                new FileData(data, header.Version),
                new FileId(header.FileName, header.Source)
            );

            shouldDelete = true;

            RemoteFiles[request.File] = remoteFile;
            request.Task.SetResult(remoteFile);
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            CloseRemoteFile(commandBuffer, request.File);

            return;
        }

        if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        if (EnableLogging) Debug.Log(string.Format("Requesting file header \"{0}\" from {1} ...", request.File.Name, request.File.Source));
        Entity rpcEntity = commandBuffer.CreateEntity();
        commandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
        {
            FileName = request.File.Name,
        });
        Entity targetConnection = request.File.Source.GetEntity(World.EntityManager);
        commandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = targetConnection,
        });
        if (EnableLogging) Debug.Log($"[{nameof(FileChunkManagerSystem)}]: Sending request for file {request.File}"); ;
    }

    void CloseRemoteFile(EntityCommandBuffer commandBuffer, FileId fileId)
    {
        Entity rpcEntity = commandBuffer.CreateEntity();
        commandBuffer.AddComponent(rpcEntity, new CloseFileRpc()
        {
            FileName = fileId.Name,
        });
        commandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = fileId.Source.GetEntity(World.EntityManager),
        });
    }

    public bool TryGetRemoteFile(FileId fileId, out RemoteFile remoteFile)
    {
        if (!Requests.Any(v => v.File == fileId) &&
            RemoteFiles.TryGetValue(fileId, out RemoteFile cached))
        {
            if (cached.Kind == FileResponseStatus.OK)
            {
                remoteFile = cached;
                return true;
            }
            else if (cached.Kind == FileResponseStatus.NotFound)
            {
                remoteFile = default;
                return false;
            }
        }
        remoteFile = default;
        return false;
    }

    public FileStatus GetRequestStatus(FileId fileId)
    {
        if (DatabaseEntity == Entity.Null)
        {
            Debug.LogWarning($"[{nameof(FileChunkManagerSystem)}]: Failed to get {nameof(BufferedFiles)} entity singleton");
            return FileStatus.Error;
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.EntityManager.GetBuffer<BufferedReceivingFile>(DatabaseEntity, true);

        for (int i = fileHeaders.Length - 1; i >= 0; i--)
        {
            BufferedReceivingFile header = fileHeaders[i];
            if (header.FileName != fileId.Name) continue;
            if (header.Source != fileId.Source) continue;

            if (header.Kind == FileResponseStatus.NotFound)
            { return FileStatus.NotFound; }

            return FileStatus.Receiving;
        }

        return FileStatus.NotRequested;
    }

    public Awaitable<RemoteFile> RequestFile(FileId fileId, IProgress<(int Current, int Total)>? progress)
    {
        for (int i = 0; i < Requests.Count; i++)
        {
            if (Requests[i].File != fileId) continue;
            return Requests[i].Task.Awaitable;
        }

        AwaitableCompletionSource<RemoteFile> task = new();
        Requests.Add(new FileRequest(fileId, task, progress));
        return task.Awaitable;
    }

    public static FileData? GetFileData(string fileName)
    {
        if (EnableLogging) Debug.Log($"Loading file data \"{fileName}\"");

        if (string.IsNullOrWhiteSpace(fileName)) return null;

        if (fileName.StartsWith("/i"))
        {
            ReadOnlySpan<char> _fileName = fileName;
            _fileName = _fileName[2..];

            if (EnableLogging) Debug.Log($"Internal \"{_fileName.ToString()}\" ...");
            if (_fileName.Consume("/e/"))
            {
                if (EnableLogging) Debug.Log($"Entity \"{_fileName.ToString()}\" ...");

                if (EnableLogging) Debug.Log($"Ghost \"{_fileName.ToString()}\" ...");

                if (!_fileName.ConsumeInt(out int ghostId))
                {
                    Debug.LogError($"Can't get ghost id");
                    return null;
                }

                if (!_fileName.Consume('_'))
                {
                    Debug.LogError($"Expected separator");
                    return null;
                }

                if (!_fileName.ConsumeUInt(out uint spawnTickValue))
                {
                    Debug.LogError($"Can't get ghost spawn tick");
                    return null;
                }

                NetworkTick spawnTick = new() { SerializedData = spawnTickValue };

                GhostInstance ghostInstance = new()
                {
                    ghostId = ghostId,
                    spawnTick = spawnTick,
                };

                EntityQuery q = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance));
                NativeArray<Entity> entities = q.ToEntityArray(Allocator.Temp);
                q.Dispose();

                Entity entity = Entity.Null;
                foreach (Entity _entity in entities)
                {
                    GhostInstance ghost = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<GhostInstance>(_entity);
                    if (ghost.ghostId != ghostInstance.ghostId) continue;
                    if (ghost.spawnTick != ghostInstance.spawnTick) continue;
                    entity = _entity;
                    break;
                }
                entities.Dispose();

                if (entity == Entity.Null)
                {
                    Debug.LogError($"Ghost {{ id: {ghostInstance.ghostId} spawnTick: {ghostInstance.spawnTick} }} not found");
                    return null;
                }

                if (_fileName.Consume("/m"))
                {
                    if (EnableLogging) Debug.Log($"Memory ...");
                    unsafe
                    {
                        Processor processor = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Processor>(entity);
                        byte[] data = new Span<byte>(&processor.Memory, Processor.TotalMemorySize).ToArray();
                        return new FileData(data, DateTime.UtcNow.Ticks);
                    }
                }
            }

            return null;
        }

        if (fileName[0] == '~')
        { fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + fileName[1..]); }

        if (!string.IsNullOrWhiteSpace(BasePath))
        {
            if (File.Exists(Path.Combine(BasePath, "." + fileName)))
            { return FileData.FromLocal(Path.Combine(BasePath, "." + fileName)); }

            if (File.Exists(Path.Combine(BasePath, fileName)))
            { return FileData.FromLocal(Path.Combine(BasePath, fileName)); }
        }

        if (File.Exists(fileName))
        { return FileData.FromLocal(fileName); }

        Debug.LogWarning($"[{nameof(FileChunkManagerSystem)}]: Local file \"{fileName}\" does not exists");
        return null;
    }

    public static int GetChunkLength(int bytes)
    {
        int n = bytes / FileChunkResponseRpc.ChunkSize;
        int rem = bytes % FileChunkResponseRpc.ChunkSize;
        if (rem != 0) n++;
        return n;
    }

    public static int GetChunkSize(int totalLength, int chunkIndex)
    {
        if (chunkIndex == FileChunkManagerSystem.GetChunkLength(totalLength) - 1)
        {
            return totalLength - (FileChunkManagerSystem.GetChunkLength(totalLength) - 1) * FileChunkResponseRpc.ChunkSize;
        }
        return FileChunkResponseRpc.ChunkSize;
    }
}
