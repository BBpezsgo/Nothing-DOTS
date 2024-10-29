using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected

public enum FileStatus
{
    Error,
    NotRequested,
    Receiving,
    Received,
}

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
        return Entity.Null;
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
        return Entity.Null;
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

public readonly struct FileData
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
        GUI.Label(rect, value.Name.ToString());
#endif
        return value;
    }

    public static bool operator ==(FileId a, FileId b) => a.Equals(b);
    public static bool operator !=(FileId a, FileId b) => !a.Equals(b);
}

public class FileChunkManager : Singleton<FileChunkManager>
{
    const bool DebugLog = false;
    public const string BasePath = "/home/BB/Projects/Nothing-DOTS/Assets/CodeFiles";

    [NotNull] Dictionary<string, FileData>? LocalFiles = default;
    [NotNull] Dictionary<FileId, FileRequest>? Requests = default;
    [NotNull] Queue<FileId>? RpcRequests = default;
    float NextCheckAt;
    Entity DatabaseEntity;

    void Start()
    {
        LocalFiles = new Dictionary<string, FileData>();
        Requests = new Dictionary<FileId, FileRequest>();
        RpcRequests = new Queue<FileId>();
        NextCheckAt = float.PositiveInfinity;
    }

    void Update()
    {
        if (NextCheckAt >= Time.time)
        {
            NextCheckAt = Time.time + 2f;

            {
                using EntityQuery databaseQ = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(BufferedFiles));
                if (databaseQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
                {
                    DatabaseEntity = bufferedFiles;
                }
                else
                {
                    Debug.LogError($"Buffered files singleton not found");
                }
            }

            foreach ((FileId file, var task) in Requests.ToArray())
            {
                (FileStatus status, int received, int total) = GetFileStatus(file, out FileData data);
                if (status == FileStatus.Received)
                {
                    task.Task.SetResult(data);
                    Requests.Remove(file);
                }
                else if (status == FileStatus.Receiving)
                {
                    task.Progress?.Report((received, total));
                }
            }

            if (RpcRequests.TryDequeue(out FileId rpcRequest))
            {
                using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);
                Entity rpcEntity = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
                {
                    FileName = rpcRequest.Name,
                });
                entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
                {
                    TargetConnection = rpcRequest.Source.GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager),
                });
                entityCommandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
                entityCommandBuffer.Dispose();
            }
        }
    }

    public static unsafe (FileStatus Status, int Received, int Total) TryGetFile(BufferedReceivingFile header, out FileData file)
    {
        file = default;

        if (Instance.DatabaseEntity == Entity.Null)
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return (FileStatus.Error, default, default);
        }

        FixedBytes126[] chunks = new FixedBytes126[FileChunkManager.GetChunkLength(header.TotalLength)];
        bool[] received = new bool[FileChunkManager.GetChunkLength(header.TotalLength)];

        DynamicBuffer<BufferedFileChunk> fileChunks = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedFileChunk>(Instance.DatabaseEntity, true);
        for (int i = 0; i < fileChunks.Length; i++)
        {
            if (fileChunks[i].TransactionId != header.TransactionId) continue;
            chunks[fileChunks[i].ChunkIndex] = fileChunks[i].Data;
            received[fileChunks[i].ChunkIndex] = true;
        }

        if (received.Any(v => !v)) return (FileStatus.Receiving, received.Count(v => v), header.TotalLength);

        byte[] data = new byte[header.TotalLength];
        for (int i = 0; i < chunks.Length; i++)
        {
            int chunkSize = FileChunkManager.GetChunkSize(header.TotalLength, i);
            Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[i]), chunkSize);
            chunk.CopyTo(data.AsSpan(i * FileChunkRpc.ChunkSize));
        }
        file = new FileData(data, header.Version);
        return (FileStatus.Received, header.TotalLength, header.TotalLength);
    }

    public static unsafe (FileStatus Status, int Received, int Total) GetFileStatus(FileId fileName, out FileData data)
    {
        data = default;

        if (Instance.DatabaseEntity == Entity.Null)
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return (FileStatus.Error, default, default);
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFile>(Instance.DatabaseEntity, true);

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            if (fileHeaders[i].FileName != fileName.Name) continue;
            if (fileHeaders[i].Source != fileName.Source) continue;

            return TryGetFile(fileHeaders[i], out data);
        }

        // Debug.LogWarning($"File {fileName} not requested");
        return (FileStatus.NotRequested, default, default);
    }

    public static Awaitable<FileData> RequestFile(FileId fileName, IProgress<(int Current, int Total)>? progress)
    {
        (FileStatus status, int received, int total) = GetFileStatus(fileName, out FileData data);
        AwaitableCompletionSource<FileData> task = new();

        if (status == FileStatus.Received)
        {
            task.SetResult(data);
            return task.Awaitable;
        }

        if (Instance.Requests.TryGetValue(fileName, out var added))
        {
            return added.Task.Awaitable;
        }

        Instance.Requests.Add(fileName, new FileRequest(task, progress));

        if (status == FileStatus.Receiving)
        {
            progress?.Report((received, total));
        }

        if (DebugLog) Debug.Log($"Requesting file \"{fileName}\"");

        Instance.RpcRequests.Enqueue(fileName);

        return task.Awaitable;
    }

    public static FileData? GetLocalFile(string fileName)
    {
        if (Instance.LocalFiles.TryGetValue(fileName, out FileData file))
        { return file; }

        if (fileName[0] == '~')
        { fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + fileName[1..]); }

        if (File.Exists(Path.Combine(BasePath, "." + fileName)))
        { return Instance.LocalFiles[fileName] = FileData.FromLocal(Path.Combine(BasePath, "." + fileName)); }

        if (File.Exists(Path.Combine(BasePath, fileName)))
        { return Instance.LocalFiles[fileName] = FileData.FromLocal(Path.Combine(BasePath, fileName)); }

        if (File.Exists(fileName))
        { return Instance.LocalFiles[fileName] = FileData.FromLocal(fileName); }

        Debug.LogWarning($"Local file \"{fileName}\" does not exists");
        return null;
    }

    public static int GetChunkLength(int bytes)
    {
        int n = bytes / FileChunkRpc.ChunkSize;
        int rem = bytes % FileChunkRpc.ChunkSize;
        if (rem != 0) n++;
        return n;
    }

    public static int GetChunkSize(int totalLength, int chunkIndex)
    {
        if (chunkIndex == FileChunkManager.GetChunkLength(totalLength) - 1)
        {
            return totalLength - (FileChunkManager.GetChunkLength(totalLength) - 1) * FileChunkRpc.ChunkSize;
        }
        return FileChunkRpc.ChunkSize;
    }
}

public readonly struct FileRequest
{
    public readonly AwaitableCompletionSource<FileData> Task;
    public readonly IProgress<(int Current, int Total)>? Progress;

    public FileRequest(AwaitableCompletionSource<FileData> task, IProgress<(int Current, int Total)>? progress)
    {
        Task = task;
        Progress = progress;
    }

    public void Deconstruct(out AwaitableCompletionSource<FileData> task, out IProgress<(int Current, int Total)>? progress)
    {
        task = Task;
        progress = Progress;
    }
}