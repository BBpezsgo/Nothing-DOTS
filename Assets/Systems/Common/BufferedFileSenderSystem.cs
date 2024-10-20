using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected
#nullable enable

partial struct BufferedFileSenderSystem : ISystem
{
    const bool DebugLog = false;

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        DynamicBuffer<BufferedSendingFile> sendingFiles = SystemAPI.GetSingletonBuffer<BufferedSendingFile>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderRequestRpc>>()
            .WithEntityAccess())
        {
            byte[]? localFile = FileChunkManager.GetLocalFile(Path.Combine(FileChunkManager.BasePath, command.ValueRO.FileName.ToString()));
            if (localFile == null)
            {
                if (DebugLog) Debug.LogError($"File \"{command.ValueRO.FileName}\" does not exists");
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            bool found = false;

            for (int i = 0; i < sendingFiles.Length; i++)
            {
                if (sendingFiles[i].FileName != command.ValueRO.FileName) continue;
                if (sendingFiles[i].Destination != request.ValueRO.SourceConnection) continue;
                
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                int totalLength = localFile.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = sendingFiles[i].FileName,
                    TransactionId = sendingFiles[i].TransactionId,
                    TotalLength = totalLength,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending file header (again) \"{sendingFiles[i].FileName}\": {{ id: {sendingFiles[i].TransactionId} }}");
                
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
                    TransactionId = command.ValueRO.FileName.GetHashCode(),
                    TotalLength = totalLength,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                sendingFiles.Add(new BufferedSendingFile(
                    request.ValueRO.SourceConnection,
                    command.ValueRO.FileName.GetHashCode(),
                    command.ValueRO.FileName
                ));
                if (DebugLog) Debug.Log($"Sending file header \"{command.ValueRO.FileName}\": {{ id: {command.ValueRO.FileName.GetHashCode()} length: {totalLength}b }}");
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileChunkRequestRpc>>()
            .WithEntityAccess())
        {
            bool found = false;

            for (int i = 0; i < sendingFiles.Length; i++)
            {
                if (sendingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;
                if (sendingFiles[i].Destination != request.ValueRO.SourceConnection) continue;
                
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                ReadOnlySpan<byte> buffer = FileChunkManager.GetLocalFile(Path.Combine(FileChunkManager.BasePath, sendingFiles[i].FileName.ToString()));
                int chunkSize = FileChunkManager.GetChunkSize(buffer.Length, command.ValueRO.ChunkIndex);
                buffer = buffer.Slice(command.ValueRO.ChunkIndex * FileChunkRpc.ChunkSize, chunkSize);
                fixed (byte* bufferPtr = buffer)
                {
                    commandBuffer.AddComponent(responseRpcEntity, new FileChunkRpc()
                    {
                        TransactionId = sendingFiles[i].TransactionId,
                        ChunkIndex = command.ValueRO.ChunkIndex,
                        Data = *(FixedBytes126*)bufferPtr,
                    });
                }
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                if (DebugLog) Debug.Log($"Sending chunk {command.ValueRO.ChunkIndex} for file {sendingFiles[i].FileName}");
                
                break;
            }

            if (!found)
            {
                if (DebugLog) Debug.LogWarning($"Can't send requested chunk for file {command.ValueRO.TransactionId}: File does not exists");
            }

            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
