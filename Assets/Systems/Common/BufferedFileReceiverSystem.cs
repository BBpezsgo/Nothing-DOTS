using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected
#nullable enable

partial struct BufferedFileReceiverSystem : ISystem
{
    public const string BasePath = "/home/BB/Projects/Nothing-DOTS/Assets/CodeFiles";
    const bool DebugLog = false;

    static readonly Dictionary<string, byte[]> FileContents = new();

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        DynamicBuffer<BufferedFileChunk> fileChunks = SystemAPI.GetSingletonBuffer<BufferedFileChunk>();
        DynamicBuffer<BufferedReceivingFile> fileHeaders = SystemAPI.GetSingletonBuffer<BufferedReceivingFile>();
        DynamicBuffer<BufferedSendingFile> fileIds = SystemAPI.GetSingletonBuffer<BufferedSendingFile>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderRpc>>()
            .WithEntityAccess())
        {
            bool added = false;
            BufferedReceivingFile fileHeader = new()
            {
                FileId = command.ValueRO.FileId,
                TotalLength = command.ValueRO.TotalLength,
                FileName = command.ValueRO.FileName,
            };

            for (int i = 0; i < fileHeaders.Length; i++)
            {
                if (fileHeaders[i].FileId == command.ValueRO.FileId)
                {
                    fileHeaders[i] = fileHeader;
                    added = true;
                    if (DebugLog) Debug.Log($"Overrided file header \"{fileHeader.FileName}\" 0/{BufferedFileReceiverSystem.GetChunkLength(fileHeader.TotalLength)}");
                    break;
                }
            }

            if (!added)
            {
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\" 0/{BufferedFileReceiverSystem.GetChunkLength(fileHeader.TotalLength)}");
                fileHeaders.Add(fileHeader);
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileChunkRpc>>()
            .WithEntityAccess())
        {
            bool added = false;
            BufferedFileChunk fileChunk = new()
            {
                FileId = command.ValueRO.FileId,
                ChunkIndex = command.ValueRO.ChunkIndex,
                Data = command.ValueRO.Data,
            };

            for (int i = 0; i < fileChunks.Length; i++)
            {
                if (fileChunks[i].ChunkIndex == fileChunk.ChunkIndex)
                {
                    fileChunks[i] = fileChunk;
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                fileChunks.Add(fileChunk);
            }

            for (int i = 0; i < fileHeaders.Length; i++)
            {
                if (fileHeaders[i].FileId != fileChunk.FileId) continue;
                fileHeaders[i] = new BufferedReceivingFile()
                {
                    FileId = fileHeaders[i].FileId,
                    FileName = fileHeaders[i].FileName,
                    TotalLength = fileHeaders[i].TotalLength,
                    LastRedeivedAt = SystemAPI.Time.ElapsedTime,
                };
                if (DebugLog) Debug.Log($"Received chunk {fileChunk.ChunkIndex}/{BufferedFileReceiverSystem.GetChunkLength(fileHeaders[i].TotalLength)} for file {fileHeaders[i].FileName}");
                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderRequestRpc>>()
            .WithEntityAccess())
        {
            bool found = false;
            for (int i = 0; i < fileHeaders.Length; i++)
            {
                if (fileHeaders[i].FileName != command.ValueRO.FileName) continue;
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = fileHeaders[i].FileName,
                    FileId = fileHeaders[i].FileId,
                    TotalLength = fileHeaders[i].TotalLength,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending file header \"{fileHeaders[i].FileName}\": {{ id: {fileHeaders[i].FileId} length: {fileHeaders[i].TotalLength}b }}");
                break;
            }

            if (found)
            {
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            byte[]? localFile = GetLocalFile(Path.Combine(BasePath, command.ValueRO.FileName.ToString()));
            if (localFile == null)
            {
                if (DebugLog) Debug.LogError($"File \"{command.ValueRO.FileName}\" does not exists");
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            for (int i = 0; i < fileIds.Length; i++)
            {
                if (fileIds[i].FileName != command.ValueRO.FileName) continue;
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                int totalLength = localFile.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = fileIds[i].FileName,
                    FileId = fileIds[i].FileId,
                    TotalLength = totalLength,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending file header \"{fileHeaders[i].FileName}\": {{ id: {fileHeaders[i].FileId} length: {fileHeaders[i].TotalLength}b }}");
                break;
            }

            if (found)
            {
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            {
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                int totalLength = localFile.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = command.ValueRO.FileName,
                    FileId = command.ValueRO.FileName.GetHashCode(),
                    TotalLength = totalLength,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                fileIds.Add(new BufferedSendingFile()
                {
                    FileId = command.ValueRO.FileName.GetHashCode(),
                    FileName = command.ValueRO.FileName,
                });
                if (DebugLog) Debug.Log($"Sending file header \"{command.ValueRO.FileName}\": {{ id: {command.ValueRO.FileName.GetHashCode()} length: {totalLength}b }}");
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileChunkRequestRpc>>()
            .WithEntityAccess())
        {
            bool found = false;
            for (int i = 0; i < fileChunks.Length; i++)
            {
                if (fileChunks[i].FileId != command.ValueRO.FileId) continue;
                if (fileChunks[i].ChunkIndex != command.ValueRO.ChunkIndex) continue;
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(responseRpcEntity, new FileChunkRpc()
                {
                    FileId = fileChunks[i].FileId,
                    ChunkIndex = fileChunks[i].ChunkIndex,
                    Data = fileChunks[i].Data,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending chunk {command.ValueRO.ChunkIndex} for file {command.ValueRO.FileId}");
                break;
            }

            if (found)
            {
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            for (int i = 0; i < fileIds.Length; i++)
            {
                if (fileIds[i].FileId != command.ValueRO.FileId) continue;
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                ReadOnlySpan<byte> buffer = GetLocalFile(Path.Combine(BasePath, fileIds[i].FileName.ToString()));
                int chunkSize = BufferedFileReceiverSystem.GetChunkSize(buffer.Length, command.ValueRO.ChunkIndex);
                buffer = buffer.Slice(command.ValueRO.ChunkIndex * FileChunkRpc.ChunkSize, chunkSize);
                fixed (byte* bufferPtr = buffer)
                {
                    commandBuffer.AddComponent(responseRpcEntity, new FileChunkRpc()
                    {
                        FileId = fileIds[i].FileId,
                        ChunkIndex = command.ValueRO.ChunkIndex,
                        Data = *(FixedBytes126*)bufferPtr,
                    });
                }
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending chunk {command.ValueRO.ChunkIndex} for file {fileIds[i].FileName}");
                break;
            }

            if (!found)
            {
                if (DebugLog) Debug.LogWarning($"Can't send requested chunk for file {command.ValueRO.FileId}: File does not exists");
            }

            commandBuffer.DestroyEntity(entity);
        }

        for (int i = 0; i < fileHeaders.Length; i++)
        {
            double delta = SystemAPI.Time.ElapsedTime - fileHeaders[i].LastRedeivedAt;
            if (delta < 1d) continue;
            fileHeaders[i] = new BufferedReceivingFile()
            {
                FileId = fileHeaders[i].FileId,
                FileName = fileHeaders[i].FileName,
                TotalLength = fileHeaders[i].TotalLength,
                LastRedeivedAt = SystemAPI.Time.ElapsedTime,
            };
            bool[] receivedChunks = new bool[BufferedFileReceiverSystem.GetChunkLength(fileHeaders[i].TotalLength)];
            for (int j = 0; j < fileChunks.Length; j++)
            {
                if (fileChunks[j].FileId != fileHeaders[i].FileId) continue;
                receivedChunks[fileChunks[j].ChunkIndex] = true;
            }

            int requested = 0;
            for (int j = 0; j < receivedChunks.Length; j++)
            {
                if (receivedChunks[j]) continue;
                Entity requestRpcEneity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(requestRpcEneity, new FileChunkRequestRpc()
                {
                    FileId = fileHeaders[i].FileId,
                    ChunkIndex = j,
                });
                commandBuffer.AddComponent(requestRpcEneity, new SendRpcCommandRequest());
                if (DebugLog) Debug.Log($"Requesting chunk {j} for file \"{fileHeaders[i].FileName}\"");
                if (requested++ >= 5) break;
            }
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }

    static byte[]? GetLocalFile(string fileName)
    {
        if (FileContents.TryGetValue(fileName, out var file))
        { return file; }
        if (!File.Exists(Path.Combine(BasePath, fileName))) return null;
        return FileContents[fileName] = File.ReadAllBytes(Path.Combine(BasePath, fileName));
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
        if (chunkIndex == BufferedFileReceiverSystem.GetChunkLength(totalLength) - 1)
        {
            return totalLength - (BufferedFileReceiverSystem.GetChunkLength(totalLength) - 1) * FileChunkRpc.ChunkSize;
        }
        return FileChunkRpc.ChunkSize;
    }
}
