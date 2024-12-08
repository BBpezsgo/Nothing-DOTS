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
    [NotNull] Queue<(FileHeaderRequestRpc, NetcodeEndPoint)>? RpcRequests = default;
    float NextCheckAt;
    Entity DatabaseEntity;

    void Start()
    {
        RemoteFiles = new();
        Requests = new();
        RpcRequests = new();
        NextCheckAt = 0f;
    }

    void Update()
    {
        if (NextCheckAt > Time.time) return;
        NextCheckAt = Time.time + 1f;

        {
            using EntityQuery databaseQ = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(BufferedFiles));
            if (!databaseQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
            {
                if (EnableLogging) Debug.LogError($"[{nameof(FileChunkManager)}]: Buffered files singleton not found");
                return;
            }

            DatabaseEntity = bufferedFiles;
        }

        for (int i = Requests.Count - 1; i >= 0; i--)
        {
            (FileId file, AwaitableCompletionSource<RemoteFile> task, IProgress<(int Current, int Total)>? progress) = Requests[i];
            (FileStatus status, int received, int total) = GetFileStatus(file, out RemoteFile data, true);
            progress?.Report((received, total));
            switch (status)
            {
                case FileStatus.Received:
                    RemoteFiles[file] = data;
                    task.SetResult(data);
                    Requests.RemoveAt(i);
                    CloseFile(file);
                    break;
                case FileStatus.Receiving:
                    break;
                case FileStatus.Error:
                    Debug.LogError($"[{nameof(FileChunkManager)}]: Error while requesting file {file.Name}");
                    RemoteFiles.Remove(file);
                    task.SetException(new Exception($"Error while requesting file {file.Name}"));
                    Requests.RemoveAt(i);
                    CloseFile(file);
                    break;
                case FileStatus.NotRequested:
                    if (RpcRequests.Count == 0)
                    {
                        RpcRequests.Enqueue((
                            new FileHeaderRequestRpc()
                            {
                                FileName = file.Name,
                            },
                            file.Source
                        ));
                    }
                    break;
                case FileStatus.NotFound:
                    Debug.LogError($"[{nameof(FileChunkManager)}]: Remote file \"{file.Name}\" not found");
                    RemoteFiles.Remove(file);
                    task.SetException(new FileNotFoundException("Remote file not found", file.Name.ToString()));
                    Requests.RemoveAt(i);
                    CloseFile(file);
                    break;
            }
        }

        if (RpcRequests.TryDequeue(out var rpcRequest))
        {
            using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);
            Entity rpcEntity = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(rpcEntity, rpcRequest.Item1);
            Entity targetConnection = rpcRequest.Item2.GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager);
            entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
            {
                TargetConnection = targetConnection,
            });
            entityCommandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
            entityCommandBuffer.Dispose();
            if (EnableLogging) Debug.Log($"[{nameof(FileChunkManager)}]: Sending file request {rpcRequest}");
        }
    }

    void CloseFile(FileId file)
    {
        using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);
        Entity rpcEntity = entityCommandBuffer.CreateEntity();
        entityCommandBuffer.AddComponent(rpcEntity, new CloseFileRpc()
        {
            FileName = file.Name,
        });
        entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = file.Source.GetEntity(World.DefaultGameObjectInjectionWorld.EntityManager),
        });
        entityCommandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
        entityCommandBuffer.Dispose();
    }

    public static unsafe (FileStatus Status, int Received, int Total) GetFileStatus(FileId fileName, out RemoteFile file, bool removeIfDone = false)
    {
        if (!Instance.Requests.Any(v => v.File == fileName) &&
            Instance.RemoteFiles.TryGetValue(fileName, out RemoteFile cached))
        {
            if (cached.Kind == FileResponseStatus.OK)
            {
                file = cached;
                return (FileStatus.Received, default, default);
            }
            else if (cached.Kind == FileResponseStatus.NotFound)
            {
                file = cached;
                return (FileStatus.NotFound, default, default);
            }
        }

        if (Instance.DatabaseEntity == Entity.Null)
        {
            Debug.LogWarning($"[{nameof(FileChunkManager)}]: Failed to get {nameof(BufferedFiles)} entity singleton");
            file = default;
            return (FileStatus.Error, default, default);
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFile>(Instance.DatabaseEntity);
        DynamicBuffer<BufferedReceivingFileChunk> fileChunks = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BufferedReceivingFileChunk>(Instance.DatabaseEntity);

        for (int i = fileHeaders.Length - 1; i >= 0; i--)
        {
            BufferedReceivingFile header = fileHeaders[i];
            if (header.FileName != fileName.Name) continue;
            if (header.Source != fileName.Source) continue;

            if (Instance.DatabaseEntity == Entity.Null)
            {
                Debug.LogWarning($"[{nameof(FileChunkManager)}]: Failed to get {nameof(BufferedFiles)} entity singleton");
                file = default;
                return (FileStatus.Error, default, default);
            }

            if (header.Kind == FileResponseStatus.NotFound)
            {
                file = new RemoteFile(
                    header.Kind,
                    new FileData(Array.Empty<byte>(), header.Version),
                    new FileId(header.FileName, header.Source)
                );
                if (removeIfDone)
                {
                    fileHeaders.RemoveAt(i);
                    for (int j = fileChunks.Length - 1; j >= 0; j--)
                    {
                        if (fileChunks[j].Source != header.Source) continue;
                        if (fileChunks[j].TransactionId != header.TransactionId) continue;
                        fileChunks.RemoveAt(j);
                    }
                }
                return (FileStatus.NotFound, default, default);
            }

            FileChunk[] chunks = new FileChunk[FileChunkManager.GetChunkLength(header.TotalLength)];
            bool[] received = new bool[FileChunkManager.GetChunkLength(header.TotalLength)];

            for (int j = 0; j < fileChunks.Length; j++)
            {
                if (fileChunks[j].TransactionId != header.TransactionId) continue;
                chunks[fileChunks[j].ChunkIndex] = fileChunks[j].Data;
                received[fileChunks[j].ChunkIndex] = true;
            }

            if (received.Any(v => !v))
            {
                file = default;
                return (FileStatus.Receiving, received.Count(v => v), received.Length);
            }

            byte[] data = new byte[header.TotalLength];
            for (int j = 0; j < chunks.Length; j++)
            {
                int chunkSize = FileChunkManager.GetChunkSize(header.TotalLength, j);
                Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[j]), chunkSize);
                chunk.CopyTo(data.AsSpan(j * FileChunkResponseRpc.ChunkSize));
            }
            file = new RemoteFile(
                header.Kind,
                new FileData(data, header.Version),
                new FileId(header.FileName, header.Source)
            );
            if (removeIfDone)
            {
                fileHeaders.RemoveAt(i);
                for (int j = fileChunks.Length - 1; j >= 0; j--)
                {
                    if (fileChunks[j].Source != header.Source) continue;
                    if (fileChunks[j].TransactionId != header.TransactionId) continue;
                    fileChunks.RemoveAt(j);
                }
            }
            return (FileStatus.Received, received.Length, received.Length);
        }

        if (EnableLogging) Debug.LogWarning($"[{nameof(FileChunkManager)}]: File {fileName} not requested");
        file = default;
        return (FileStatus.NotRequested, default, default);
    }

    public static Awaitable<RemoteFile> RequestFile(FileId fileName, IProgress<(int Current, int Total)>? progress, bool force)
    {
        (FileStatus status, int received, int total) = GetFileStatus(fileName, out RemoteFile data, true);
        AwaitableCompletionSource<RemoteFile> task = new();

        if (status == FileStatus.Received && !force)
        {
            task.SetResult(data);
            return task.Awaitable;
        }

        for (int i = 0; i < Instance.Requests.Count; i++)
        {
            if (Instance.Requests[i].File != fileName) continue;
            return Instance.Requests[i].Task.Awaitable;
        }

        Instance.Requests.Add(new FileRequest(fileName, task, progress));

        progress?.Report((received, total));
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

    public FileRequest(FileId file, AwaitableCompletionSource<RemoteFile> task, IProgress<(int Current, int Total)>? progress)
    {
        File = file;
        Task = task;
        Progress = progress;
    }

    public void Deconstruct(out FileId file, out AwaitableCompletionSource<RemoteFile> task, out IProgress<(int Current, int Total)>? progress)
    {
        file = File;
        task = Task;
        progress = Progress;
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
