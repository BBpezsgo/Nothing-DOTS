using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected
#nullable enable

public enum FileStatus
{
    Error,
    Receiving,
    Received,
}

public readonly struct FileId : IEquatable<FileId>
{
    public readonly FixedString64Bytes Name;
    public readonly Entity Source;

    public FileId(FixedString64Bytes name, Entity source)
    {
        Name = name;
        Source = source;
    }

    public override int GetHashCode() => HashCode.Combine(Name, Source);
    public override string ToString() => $"{Source} {Name}";
    public override bool Equals(object obj) => obj is FileId other && Equals(other);
    public bool Equals(FileId other) => Name.Equals(other.Name) && Source.Equals(other.Source);

    public static bool operator ==(FileId a, FileId b) => a.Equals(b);
    public static bool operator !=(FileId a, FileId b) => !a.Equals(b);
}

public class FileChunkManager : Singleton<FileChunkManager>
{
    const bool DebugLog = true;
    public const string BasePath = "/home/BB/Projects/Nothing-DOTS/Assets/CodeFiles";

    Dictionary<string, byte[]> LocalFiles;
    Dictionary<FileId, Action<byte[]>?> Requests;
    float NextCheckAt;

    void Start()
    {
        LocalFiles = new Dictionary<string, byte[]>();
        Requests = new Dictionary<FileId, Action<byte[]>?>();
        NextCheckAt = float.PositiveInfinity;
    }

    void Update()
    {
        if (NextCheckAt >= Time.time)
        {
            NextCheckAt = Time.time + 2f;

            foreach (KeyValuePair<FileId, Action<byte[]>?> request in Requests.ToArray())
            {
                if (TryGetFile(request.Key, out var data) == FileStatus.Received)
                {
                    request.Value?.Invoke(data!);
                    Requests.Remove(request.Key);
                }
            }
        }
    }

    public static unsafe FileStatus TryGetFile(BufferedReceivingFile header, out byte[]? data)
    {
        data = null;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        FixedBytes126[] chunks = new FixedBytes126[FileChunkManager.GetChunkLength(header.TotalLength)];
        bool[] received = new bool[FileChunkManager.GetChunkLength(header.TotalLength)];

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return FileStatus.Error;
        }

        DynamicBuffer<BufferedFileChunk> fileChunks = entityManager.GetBuffer<BufferedFileChunk>(bufferedFiles, true);
        for (int i = 0; i < fileChunks.Length; i++)
        {
            if (fileChunks[i].TransactionId != header.TransactionId) continue;
            chunks[fileChunks[i].ChunkIndex] = fileChunks[i].Data;
            received[fileChunks[i].ChunkIndex] = true;
        }

        if (received.Any(v => !v)) return FileStatus.Receiving;

        data = new byte[header.TotalLength];
        for (int i = 0; i < chunks.Length; i++)
        {
            int chunkSize = FileChunkManager.GetChunkSize(header.TotalLength, i);
            Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[i]), chunkSize);
            chunk.CopyTo(data.AsSpan(i * FileChunkRpc.ChunkSize));
        }
        return FileStatus.Received;
    }

    public static unsafe FileStatus TryGetFile(FileId fileName, out byte[]? data)
    {
        data = null;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return FileStatus.Error;
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = entityManager.GetBuffer<BufferedReceivingFile>(bufferedFiles);

        BufferedReceivingFile fileHeader = default;

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            fileHeader = fileHeaders[i];
            if (fileHeader.FileName != fileName.Name) continue;
            if (fileHeader.Source != fileName.Source) continue;

            return TryGetFile(fileHeader, out data);
        }

        return FileStatus.Error;
    }

    public static unsafe void TryGetFile(FileId fileName, Action<byte[]>? callback, EntityCommandBuffer entityCommandBuffer)
    {
        var status = TryGetFile(fileName, out var data);
        if (status == FileStatus.Received)
        {
            callback?.Invoke(data!);
            return;
        }
        else if (status == FileStatus.Receiving)
        {
            Instance.Requests[fileName] = callback;
            return;
        }

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return;
        }

        if (DebugLog) Debug.Log($"Requesting file \"{fileName}\"");

        Instance.Requests[fileName] = callback;

        Entity rpcEntity = entityCommandBuffer.CreateEntity();
        entityCommandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
        {
            FileName = fileName.Name,
        });
        entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest()
        {
            TargetConnection = fileName.Source,
        });
    }

    public static byte[]? GetLocalFile(string fileName)
    {
        if (Instance.LocalFiles.TryGetValue(fileName, out var file))
        { return file; }
        if (File.Exists(Path.Combine(BasePath, "." + fileName)))
        { return Instance.LocalFiles[fileName] = File.ReadAllBytes(Path.Combine(BasePath, "." + fileName)); }
        if (File.Exists(Path.Combine(BasePath, fileName)))
        { return Instance.LocalFiles[fileName] = File.ReadAllBytes(Path.Combine(BasePath, fileName)); }
        if (File.Exists(fileName))
        { return Instance.LocalFiles[fileName] = File.ReadAllBytes(fileName); }
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
