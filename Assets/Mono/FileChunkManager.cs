#pragma warning disable CS0162 // Unreachable code detected

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public enum FileStatus
{
    Error,
    NotRequested,
    Receiving,
    Received,
    NotFound,
}

public static class FileStatusExtensions
{
    public static bool IsOk(this FileStatus status) =>
        status is
        FileStatus.Received;
}

[BurstCompile]
public struct NetcodeEndPoint : IEquatable<NetcodeEndPoint>, IRpcCommandSerializer<NetcodeEndPoint>, IComponentData
{
    public NetworkId ConnectionId;
    public Entity ConnectionEntity;
    public readonly bool IsServer => ConnectionId.Value == default && ConnectionEntity == default;

    public static NetcodeEndPoint Server => new(default, default);

    public NetcodeEndPoint(NetworkId connectionId, Entity connectionEntity)
    {
        ConnectionId = connectionId;
        ConnectionEntity = connectionEntity;
    }

    public Entity GetEntity()
        => GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager);
    public Entity GetEntity(EntityManager entityManager)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        using EntityQuery entityQ = entityManager.CreateEntityQuery(typeof(NetworkId));
        using NativeArray<Entity> entities = entityQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            NetworkId networkId = entityManager.GetComponentData<NetworkId>(entities[i]);
            if (networkId.Value != ConnectionId.Value) continue;
            return ConnectionEntity = entities[i];
        }
        throw new Exception($"Connection entity {ConnectionId} not found");
    }

    public Entity GetEntity(ref SystemState state)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        EntityQuery entityQ = state.GetEntityQuery(typeof(NetworkId));
        ComponentLookup<NetworkId> componentQ = state.GetComponentLookup<NetworkId>(true);
        return GetEntity(entityQ, componentQ);
    }
    public Entity GetEntity(in EntityQuery entityQ, in ComponentLookup<NetworkId> componentQ)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        using NativeArray<Entity> entities = entityQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            RefRO<NetworkId> networkId = componentQ.GetRefRO(entities[i]);
            if (networkId.ValueRO.Value != ConnectionId.Value) continue;
            return ConnectionEntity = entities[i];
        }
        throw new Exception($"Connection entity {ConnectionId} not found");
    }

    public override readonly bool Equals(object obj) => obj is NetcodeEndPoint other && Equals(other);
    public readonly bool Equals(NetcodeEndPoint other) => ConnectionId.Value == other.ConnectionId.Value;
    public override readonly int GetHashCode() => ConnectionId.Value;
    public override readonly string ToString() => IsServer ? "SERVER" : ConnectionId.ToString();

    public readonly void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in NetcodeEndPoint data)
    {
        writer.WriteInt(data.ConnectionId.Value);
    }

    public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref NetcodeEndPoint data)
    {
        data.ConnectionId = new NetworkId()
        {
            Value = reader.ReadInt()
        };
    }

    static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer = new(InvokeExecute);
    public readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute() => InvokeExecuteFunctionPointer;
    [BurstCompile(DisableDirectCall = true)]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters) => RpcExecutor.ExecuteCreateRequestComponent<NetcodeEndPoint, NetcodeEndPoint>(ref parameters);

    public static bool operator ==(NetcodeEndPoint a, NetcodeEndPoint b) => a.ConnectionId.Value == b.ConnectionId.Value;
    public static bool operator !=(NetcodeEndPoint a, NetcodeEndPoint b) => a.ConnectionId.Value != b.ConnectionId.Value;
}

public readonly struct FileData : IInspect<FileData>
{
    public readonly byte[] Data;
    public readonly long Version;

    public FileData(byte[] data, long version)
    {
        Data = data;
        Version = version;
    }

    public static FileData FromLocal(string localFile)
        => new(File.ReadAllBytes(localFile), File.GetLastWriteTimeUtc(localFile).Ticks);

    public FileData OnGUI(Rect rect, FileData value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.Label(rect, $"{value.Data.Length} bytes");
        GUI.enabled = t;
#endif
        return value;
    }
}

public struct FileId : IEquatable<FileId>, IInspect<FileId>
{
    public FixedString64Bytes Name;
    public NetcodeEndPoint Source;

    public FileId(FixedString64Bytes name, NetcodeEndPoint source)
    {
        Name = name;
        Source = source;
    }

    public override readonly int GetHashCode() => HashCode.Combine(Name, Source);
    public override readonly string ToString() => $"{Source} {Name}";
    public override readonly bool Equals(object obj) => obj is FileId other && Equals(other);
    public readonly bool Equals(FileId other) => Name.Equals(other.Name) && Source.Equals(other.Source);

    public readonly FileId OnGUI(Rect rect, FileId value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.TextField(rect, value.Name.ToString());
        GUI.enabled = t;
#endif
        return value;
    }

    public static bool operator ==(FileId a, FileId b) => a.Equals(b);
    public static bool operator !=(FileId a, FileId b) => !a.Equals(b);
}

public readonly struct RemoteFile : IInspect<RemoteFile>
{
    public readonly FileResponseStatus Kind;
    public readonly FileData File;
    public readonly FileId Source;

    public RemoteFile(FileResponseStatus kind, FileData file, FileId source)
    {
        Kind = kind;
        File = file;
        Source = source;
    }

    public RemoteFile OnGUI(Rect rect, RemoteFile value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.Label(rect, value.Kind.ToString());
        GUI.enabled = true;
#endif
        return value;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(SerializableDictionary<string, FileData>))]
public class _Drawer2 : DictionaryDrawer<string, FileData> { }
[UnityEditor.CustomPropertyDrawer(typeof(SerializableDictionary<FileId, RemoteFile>))]
public class _Drawer3 : DictionaryDrawer<FileId, RemoteFile> { }
[UnityEditor.CustomPropertyDrawer(typeof(SerializableDictionary<FileId, FileRequest>))]
public class _Drawer4 : DictionaryDrawer<FileId, FileRequest> { }
#endif

public class FileChunkManager : Singleton<FileChunkManager>
{
    const bool EnableLogging = false;
    public static string? BasePath => Application.streamingAssetsPath;

    [SerializeField, NotNull] SerializableDictionary<FileId, RemoteFile>? RemoteFiles = default;
    [SerializeField, NotNull] List<FileRequest>? Requests = default;
    float NextCheckAt;
    Entity DatabaseEntity;

    void Start()
    {
        RemoteFiles = new();
        Requests = new();
        NextCheckAt = 0f;
    }

    void Update()
    {
        if (NextCheckAt > Time.time || Requests.Count == 0) return;
        NextCheckAt = Time.time + .2f;

        {
            using EntityQuery databaseQ = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(BufferedFiles));
            if (!databaseQ.TryGetSingletonEntity<BufferedFiles>(out DatabaseEntity))
            {
                if (EnableLogging) Debug.LogError($"[{nameof(FileChunkManager)}]: Buffered files singleton not found");
                return;
            }
        }

        EntityCommandBuffer commandBuffer = default;

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFile>(DatabaseEntity);
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFileChunk>(DatabaseEntity);

        for (int i = Requests.Count - 1; i >= 0; i--)
        {
            HandleRequest(
                ref commandBuffer,
                fileHeaders,
                fileChunks,
                Requests[i],
                out bool shouldDelete
            );

            if (shouldDelete)
            {
                Requests.RemoveAt(i);
                DeleteRequest(
                    i,
                    fileHeaders,
                    fileChunks
                );
            }
        }

        if (commandBuffer.IsCreated)
        {
            commandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
            commandBuffer.Dispose();
        }
    }

    static void DeleteRequest(
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
        out bool shouldDelete)
    {
        shouldDelete = false;

        for (int i = fileHeaders.Length - 1; i >= 0; i--)
        {
            BufferedReceivingFile header = fileHeaders[i];
            if (header.FileName != request.File.Name) continue;
            if (header.Source != request.File.Source) continue;

            if (header.Kind == FileResponseStatus.NotFound)
            {
                shouldDelete = true;

                Debug.LogError($"[{nameof(FileChunkManager)}]: Remote file \"{request.File.Name}\" not found");
                RemoteFiles.Remove(request.File);
                request.Progress?.Report(default);
                request.Task.SetException(new FileNotFoundException("Remote file not found", request.File.Name.ToString()));
                if (!commandBuffer.IsCreated) commandBuffer = new(Allocator.Temp);
                CloseRemoteFile(commandBuffer, request.File);

                return;
            }

            int totalLength = FileChunkManager.GetChunkLength(header.TotalLength);

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
                int chunkSize = FileChunkManager.GetChunkSize(header.TotalLength, j);
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
            if (!commandBuffer.IsCreated) commandBuffer = new(Allocator.Temp);
            CloseRemoteFile(commandBuffer, request.File);

            return;
        }

        if (!commandBuffer.IsCreated) commandBuffer = new(Allocator.Temp);

        Entity rpcEntity = commandBuffer.CreateEntity();
        commandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
        {
            FileName = request.File.Name,
        });
        Entity targetConnection = request.File.Source.GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager);
        commandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = targetConnection,
        });
        if (EnableLogging) Debug.Log($"[{nameof(FileChunkManager)}]: Sending request for file {request.File}"); ;
    }

    static void CloseRemoteFile(EntityCommandBuffer commandBuffer, FileId fileId)
    {
        Entity rpcEntity = commandBuffer.CreateEntity();
        commandBuffer.AddComponent(rpcEntity, new CloseFileRpc()
        {
            FileName = fileId.Name,
        });
        commandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = fileId.Source.GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager),
        });
    }

    public static bool TryGetRemoteFile(FileId fileId, out RemoteFile remoteFile)
    {
        if (!Instance.Requests.Any(v => v.File == fileId) &&
            Instance.RemoteFiles.TryGetValue(fileId, out RemoteFile cached))
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

    public static FileStatus GetRequestStatus(FileId fileId)
    {
        if (Instance.DatabaseEntity == Entity.Null)
        {
            Debug.LogWarning($"[{nameof(FileChunkManager)}]: Failed to get {nameof(BufferedFiles)} entity singleton");
            return FileStatus.Error;
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFile>(Instance.DatabaseEntity, true);

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

    public static Awaitable<RemoteFile> RequestFile(FileId fileId, IProgress<(int Current, int Total)>? progress)
    {
        for (int i = 0; i < Instance.Requests.Count; i++)
        {
            if (Instance.Requests[i].File != fileId) continue;
            return Instance.Requests[i].Task.Awaitable;
        }

        AwaitableCompletionSource<RemoteFile> task = new();
        Instance.Requests.Add(new FileRequest(fileId, task, progress));
        return task.Awaitable;
    }

    public static FileData? GetLocalFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

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

        Debug.LogWarning($"[{nameof(FileChunkManager)}]: Local file \"{fileName}\" does not exists");
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
        if (chunkIndex == FileChunkManager.GetChunkLength(totalLength) - 1)
        {
            return totalLength - (FileChunkManager.GetChunkLength(totalLength) - 1) * FileChunkResponseRpc.ChunkSize;
        }
        return FileChunkResponseRpc.ChunkSize;
    }
}

public readonly struct FileRequest : IInspect<FileRequest>
{
    public readonly FileId File;
    public readonly AwaitableCompletionSource<RemoteFile> Task;
    public readonly IProgress<(int Current, int Total)>? Progress;

    public FileRequest(
        FileId file,
        AwaitableCompletionSource<RemoteFile> task,
        IProgress<(int Current, int Total)>? progress)
    {
        File = file;
        Task = task;
        Progress = progress;
    }

    public FileRequest OnGUI(Rect rect, FileRequest value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        if (value.Progress is ProgressRecord<(int Current, int Total)> progressRecord)
        {
            UnityEditor.EditorGUI.ProgressBar(rect, progressRecord.Progress.Total == 0 ? 0f : (float)progressRecord.Progress.Current / (float)progressRecord.Progress.Total, File.ToString());
        }
        else
        {
            GUI.Label(rect, File.ToString());
        }
        GUI.enabled = true;
#endif
        return value;
    }
}
