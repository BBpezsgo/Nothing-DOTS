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

partial class FileChunkManagerSystem : SystemBase
{
    const bool EnableLogging = false;
    public static string? BasePath => Application.streamingAssetsPath;

    [NotNull] readonly Dictionary<FileId, RemoteFile>? RemoteFiles = new();
    [NotNull] readonly List<FileRequest>? Requests = new();

    public static FileChunkManagerSystem GetInstance(World world)
        => world.GetExistingSystemManaged<FileChunkManagerSystem>();

    protected override void OnCreate()
    {
        RequireForUpdate<BufferedFiles>();
    }

    protected override void OnUpdate()
    {
        if (Requests.Count == 0) return;

        EntityCommandBuffer commandBuffer = default;

        Entity databaseEntity = SystemAPI.GetSingletonEntity<BufferedFiles>();
        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.EntityManager.GetBuffer<BufferedReceivingFile>(databaseEntity);
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks = World.EntityManager.GetBuffer<BufferedReceivingFileChunk>(databaseEntity);

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
        FileRequest request,
        out bool shouldDelete,
        out int headerIndex)
    {
        shouldDelete = false;
        headerIndex = -1;

        bool requestCached = true;

        for (int i = fileHeaders.Length - 1; i >= 0; i--)
        {
            BufferedReceivingFile header = fileHeaders[i];
            if (header.FileName != request.File.Name) continue;
            if (header.Source != request.File.Source) continue;

            headerIndex = i;

            if (header.Kind == FileResponseStatus.NotFound)
            {
                shouldDelete = true;

                Debug.LogWarning($"[{nameof(FileChunkManagerSystem)}]: Remote file \"{request.File.ToUri()}\" not found");
                RemoteFiles.Remove(request.File);
                request.Task.SetException(new FileNotFoundException("Remote file not found", request.File.Name.ToString()));
                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                CloseRemoteFile(commandBuffer, request.File);

                return;
            }
            else if (header.Kind == FileResponseStatus.NotChanged)
            {
                if (RemoteFiles.TryGetValue(request.File, out RemoteFile remoteFile))
                {
                    shouldDelete = true;

                    request.Task.SetResult(remoteFile);
                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    CloseRemoteFile(commandBuffer, request.File);

                    if (EnableLogging) Debug.Log($"[{nameof(FileChunkManagerSystem)}]: Remote file \"{request.File.ToUri()}\" was not changed");

                    return;
                }

                Debug.LogWarning($"[{nameof(FileChunkManagerSystem)}]: Remote file \"{request.File.ToUri()}\" was not changed but not found locally, requesting without cache ...");
                requestCached = false;
            }
            else if (header.Kind == FileResponseStatus.OK)
            {
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
        }

        if (DateTime.UtcNow.TimeOfDay.TotalSeconds - request.RequestSentAt > 5d)
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            Entity rpcEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
            {
                FileName = request.File.Name,
                Version = requestCached && RemoteFiles.TryGetValue(request.File, out RemoteFile v) ? v.File.Version : 0,
            });
            Entity targetConnection = request.File.Source.GetEntity(World.EntityManager);
            commandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
            {
                TargetConnection = targetConnection,
            });
            request.RequestSent();
            if (EnableLogging) Debug.Log($"[{nameof(FileChunkManagerSystem)}]: Sending request for file \"{request.File.ToUri()}\"");
        }
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
        if (Requests.Any(v => v.File == fileId))
        {
            return FileStatus.Receiving;
        }

        if (RemoteFiles.TryGetValue(fileId, out RemoteFile status))
        {
            return status.Kind switch
            {
                FileResponseStatus.OK => FileStatus.Received,
                FileResponseStatus.NotFound => FileStatus.NotFound,
                FileResponseStatus.NotChanged => FileStatus.Received,
                _ => throw new UnreachableException(),
            };
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
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        if (fileName.StartsWith("/i"))
        {
            ReadOnlySpan<char> _fileName = fileName;
            _fileName = _fileName[2..];

            if (_fileName.Consume("/e/"))
            {
                if (!_fileName.ConsumeInt(out int ghostId))
                {
                    Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Can't get ghost id");
                    return null;
                }

                if (!_fileName.Consume('_'))
                {
                    Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Expected separator");
                    return null;
                }

                if (!_fileName.ConsumeUInt(out uint spawnTickValue))
                {
                    Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Can't get ghost spawn tick");
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
                    Debug.LogError($"[{nameof(FileChunkManagerSystem)}]: Ghost {{ id: {ghostInstance.ghostId} spawnTick: {ghostInstance.spawnTick} }} not found");
                    return null;
                }

                if (_fileName.Consume("/m"))
                {
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
        if (chunkIndex == GetChunkLength(totalLength) - 1)
        {
            return totalLength - (GetChunkLength(totalLength) - 1) * FileChunkResponseRpc.ChunkSize;
        }
        return FileChunkResponseRpc.ChunkSize;
    }
}
