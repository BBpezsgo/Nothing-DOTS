using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected

partial struct BufferedFileReceiverSystem : ISystem
{
    const bool DebugLog = false;

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        DynamicBuffer<BufferedFileChunk> fileChunks = SystemAPI.GetSingletonBuffer<BufferedFileChunk>();
        DynamicBuffer<BufferedReceivingFile> receivingFiles = SystemAPI.GetSingletonBuffer<BufferedReceivingFile>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderRpc>>()
            .WithEntityAccess())
        {
            bool added = false;
            BufferedReceivingFile fileHeader = new(
                request.ValueRO.SourceConnection,
                command.ValueRO.TransactionId,
                command.ValueRO.FileName,
                command.ValueRO.TotalLength,
                SystemAPI.Time.ElapsedTime,
                command.ValueRO.Version
            );

            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].Source != request.ValueRO.SourceConnection) continue;
                if (receivingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;

                receivingFiles[i] = fileHeader;
                added = true;
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\" (again)");

                break;
            }

            if (!added)
            {
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\"");
                receivingFiles.Add(fileHeader);
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileChunkRpc>>()
            .WithEntityAccess())
        {
            bool added = false;
            BufferedFileChunk fileChunk = new(
                request.ValueRO.SourceConnection,
                command.ValueRO.TransactionId,
                command.ValueRO.ChunkIndex,
                command.ValueRO.Data
            );

            for (int i = 0; i < fileChunks.Length; i++)
            {
                if (fileChunks[i].Source != fileChunk.Source) continue;
                if (fileChunks[i].TransactionId != fileChunk.TransactionId) continue;
                if (fileChunks[i].ChunkIndex != fileChunk.ChunkIndex) continue;

                fileChunks[i] = fileChunk;
                added = true;
                if (DebugLog) Debug.Log($"Received chunk {fileChunk.ChunkIndex} (again)");
                break;
            }

            if (!added)
            {
                fileChunks.Add(fileChunk);
                if (DebugLog) Debug.Log($"Received chunk {fileChunk.ChunkIndex}");
            }

            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].Source != request.ValueRO.SourceConnection) continue;
                if (receivingFiles[i].TransactionId != fileChunk.TransactionId) continue;

                receivingFiles[i] = new BufferedReceivingFile(
                    receivingFiles[i].Source,
                    receivingFiles[i].TransactionId,
                    receivingFiles[i].FileName,
                    receivingFiles[i].TotalLength,
                    SystemAPI.Time.ElapsedTime,
                    receivingFiles[i].Version
                );
                if (DebugLog) Debug.Log($"{receivingFiles[i].FileName} {fileChunk.ChunkIndex}/{FileChunkManager.GetChunkLength(receivingFiles[i].TotalLength)}");

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        int requested = 0;
        for (int i = 0; i < receivingFiles.Length; i++)
        {
            double delta = SystemAPI.Time.ElapsedTime - receivingFiles[i].LastRedeivedAt;
            if (delta < 0.5d) continue;

            bool[] receivedChunks = new bool[FileChunkManager.GetChunkLength(receivingFiles[i].TotalLength)];
            for (int j = 0; j < fileChunks.Length; j++)
            {
                if (fileChunks[j].Source != receivingFiles[i].Source) continue;
                if (fileChunks[j].TransactionId != receivingFiles[i].TransactionId) continue;

                receivedChunks[fileChunks[j].ChunkIndex] = true;
            }

            for (int j = 0; j < receivedChunks.Length; j++)
            {
                if (receivedChunks[j]) continue;
                Entity requestRpcEneity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(requestRpcEneity, new FileChunkRequestRpc
                {
                    TransactionId = receivingFiles[i].TransactionId,
                    ChunkIndex = j,
                });
                commandBuffer.AddComponent(requestRpcEneity, new SendRpcCommandRequest
                {
                    TargetConnection = receivingFiles[i].Source
                });
                if (DebugLog) Debug.Log($"Requesting chunk {j} for file \"{receivingFiles[i].FileName}\"");
                if (requested++ >= 5) break;
            }

            if (requested == 0) continue;

            receivingFiles[i] = new BufferedReceivingFile(
                receivingFiles[i].Source,
                receivingFiles[i].TransactionId,
                receivingFiles[i].FileName,
                receivingFiles[i].TotalLength,
                SystemAPI.Time.ElapsedTime,
                receivingFiles[i].Version
            );
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
