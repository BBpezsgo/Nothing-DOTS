using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#nullable enable

public class FileChunkManager : Singleton<FileChunkManager>
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log(TryGetFile("test.bbc", out byte[]? buffer) ? Encoding.UTF8.GetString(buffer) : "null");
        }
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
        DynamicBuffer<BufferedFileChunk> fileChunks = entityManager.GetBuffer<BufferedFileChunk>(bufferedFiles, true);

        bool found = false;
        BufferedReceivingFile fileHeader = default;

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            fileHeader = fileHeaders[i];
            if (fileHeader.FileName != fileName) continue;
            found = true;
            break;
        }

        if (!found)
        {
            Debug.Log($"Requesting file \"{fileName}\"");

            using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);

            Entity rpcEntity = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(rpcEntity, new FileHeaderRequestRpc()
            {
                FileName = fileName,
            });
            entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest());

            entityCommandBuffer.Playback(entityManager);
            return false;
        }

        FixedBytes126[] chunks = new FixedBytes126[BufferedFileReceiverSystem.GetChunkLength(fileHeader.TotalLength)];
        int received = 0;

        for (int j = 0; j < fileChunks.Length; j++)
        {
            if (fileChunks[j].FileId != fileHeader.FileId) continue;
            chunks[fileChunks[j].ChunkIndex] = fileChunks[j].Data;
            received++;
        }

        if (received > chunks.Length)
        {
            Debug.LogWarning($"Too much chunks received ({received}) for file \"{fileHeader.FileName}\" ({chunks.Length})");
            return false;
        }

        if (received < chunks.Length)
        {
            // Debug.Log($"\"{fileHeader.FileName}\" {received}/{chunks.Length}");
            return false;
        }

        data = new byte[fileHeader.TotalLength];
        for (int i = 0; i < chunks.Length; i++)
        {
            int chunkSize = BufferedFileReceiverSystem.GetChunkSize(fileHeader.TotalLength, i);
            Span<byte> chunk = new(Unsafe.AsPointer(ref chunks[i]), chunkSize);
            chunk.CopyTo(data.AsSpan(i * FileChunkRpc.ChunkSize));
        }
        return true;
    }
}
