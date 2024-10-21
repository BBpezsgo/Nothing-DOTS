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

public class FileChunkManager : Singleton<FileChunkManager>
{
    const bool DebugLog = false;
    public const string BasePath = "/home/BB/Projects/Nothing-DOTS/Assets/CodeFiles";

    public Dictionary<string, byte[]> FileContents;
    public Dictionary<string, Action<byte[]>> Requests;
    float NextCheckAt;

    void Start()
    {
        FileContents = new Dictionary<string, byte[]>();
        Requests = new Dictionary<string, Action<byte[]>>();
        NextCheckAt = float.PositiveInfinity;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryGetFile("test.bbc", (buffer) =>
            {
                Debug.Log(Encoding.UTF8.GetString(buffer));
            });
        }

        if (NextCheckAt >= Time.time)
        {
            NextCheckAt = Time.time + 2f;

            foreach (KeyValuePair<string, Action<byte[]>> request in Requests.ToArray())
            {
                if (TryGetFile(request.Key, out var data))
                {
                    request.Value.Invoke(data);
                    Requests.Remove(request.Key);
                }
            }
        }
    }

    public static unsafe bool TryGetFile(BufferedReceivingFile header, [NotNullWhen(true)] out byte[]? data)
    {
        data = null;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        FixedBytes126[] chunks = new FixedBytes126[FileChunkManager.GetChunkLength(header.TotalLength)];
        bool[] received = new bool[FileChunkManager.GetChunkLength(header.TotalLength)];

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return false;
        }

        DynamicBuffer<BufferedFileChunk> fileChunks = entityManager.GetBuffer<BufferedFileChunk>(bufferedFiles, true);
        for (int i = 0; i < fileChunks.Length; i++)
        {
            if (fileChunks[i].TransactionId != header.TransactionId) continue;
            chunks[fileChunks[i].ChunkIndex] = fileChunks[i].Data;
            received[fileChunks[i].ChunkIndex] = true;
        }

        if (received.Any(v => !v)) return false;

        data = new byte[header.TotalLength];
        for (int i = 0; i < chunks.Length; i++)
        {
            int chunkSize = FileChunkManager.GetChunkSize(header.TotalLength, i);
            Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[i]), chunkSize);
            chunk.CopyTo(data.AsSpan(i * FileChunkRpc.ChunkSize));
        }
        return true;
    }

    public static unsafe bool TryGetFile(string fileName, [NotNullWhen(true)] out byte[]? data)
    {
        data = null;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return false;
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = entityManager.GetBuffer<BufferedReceivingFile>(bufferedFiles);

        BufferedReceivingFile fileHeader = default;

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            fileHeader = fileHeaders[i];
            if (fileHeader.FileName != fileName) continue;

            return TryGetFile(fileHeader, out data);
        }

        return false;
    }

    public static unsafe void TryGetFile(string fileName, Action<byte[]> callback)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        using EntityQuery bufferedFilesQ = entityManager.CreateEntityQuery(typeof(BufferedFiles));
        if (!bufferedFilesQ.TryGetSingletonEntity<BufferedFiles>(out Entity bufferedFiles))
        {
            Debug.LogWarning($"Failed to get {nameof(BufferedFiles)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedReceivingFile> fileHeaders = entityManager.GetBuffer<BufferedReceivingFile>(bufferedFiles);

        BufferedReceivingFile fileHeader = default;

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            fileHeader = fileHeaders[i];
            if (fileHeader.FileName != fileName) continue;

            if (TryGetFile(fileHeader, out var data))
            {
                callback.Invoke(data);
                return;
            }

            break;
        }

        if (DebugLog) Debug.Log($"Requesting file \"{fileName}\"");

        using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);

        Instance.Requests[fileName] = callback;

        Entity rpcEntity = entityCommandBuffer.CreateEntity();
        entityCommandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
        {
            FileName = fileName,
        });
        entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest());

        entityCommandBuffer.Playback(entityManager);
        return;
    }

    public static byte[]? GetLocalFile(string fileName)
    {
        if (Instance.FileContents.TryGetValue(fileName, out var file))
        { return file; }
        if (!File.Exists(Path.Combine(BasePath, fileName))) return null;
        return Instance.FileContents[fileName] = File.ReadAllBytes(Path.Combine(BasePath, fileName));
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
