using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected
#nullable enable

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
                SystemAPI.Time.ElapsedTime
            );

            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;
                if (receivingFiles[i].Source != request.ValueRO.SourceConnection) continue;

                receivingFiles[i] = fileHeader;
                added = true;
                if (DebugLog) Debug.Log($"Overrided file header \"{fileHeader.FileName}\" 0/{FileChunkManager.GetChunkLength(fileHeader.TotalLength)}");

                break;
            }

            if (!added)
            {
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\" 0/{FileChunkManager.GetChunkLength(fileHeader.TotalLength)}");
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
                if (fileChunks[i].ChunkIndex == fileChunk.ChunkIndex)
                {
                    fileChunks[i] = fileChunk;
                    added = true;
                    if (DebugLog) Debug.Log($"Received chunk (again) {fileChunk.ChunkIndex}/{FileChunkManager.GetChunkLength(receivingFiles[i].TotalLength)} for file {receivingFiles[i].FileName}");
                    break;
                }
            }

            if (!added)
            {
                fileChunks.Add(fileChunk);
            }

            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].TransactionId != fileChunk.TransactionId) continue;
                if (receivingFiles[i].Source != request.ValueRO.SourceConnection) continue;

                receivingFiles[i] = new BufferedReceivingFile(
                    receivingFiles[i].Source,
                    receivingFiles[i].TransactionId,
                    receivingFiles[i].FileName,
                    receivingFiles[i].TotalLength,
                    SystemAPI.Time.ElapsedTime
                );
                if (DebugLog) if (!added) Debug.Log($"Received chunk {fileChunk.ChunkIndex}/{FileChunkManager.GetChunkLength(receivingFiles[i].TotalLength)} for file {receivingFiles[i].FileName}");

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        for (int i = 0; i < receivingFiles.Length; i++)
        {
            double delta = SystemAPI.Time.ElapsedTime - receivingFiles[i].LastRedeivedAt;
            if (delta < 1d) continue;

            bool[] receivedChunks = new bool[FileChunkManager.GetChunkLength(receivingFiles[i].TotalLength)];
            for (int j = 0; j < fileChunks.Length; j++)
            {
                if (fileChunks[j].TransactionId != receivingFiles[i].TransactionId) continue;
                if (fileChunks[j].Source != receivingFiles[i].Source) continue;

                receivedChunks[fileChunks[j].ChunkIndex] = true;
            }

            int requested = 0;
            for (int j = 0; j < receivedChunks.Length; j++)
            {
                if (receivedChunks[j]) continue;
                Entity requestRpcEneity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(requestRpcEneity, new FileChunkRequestRpc()
                {
                    TransactionId = receivingFiles[i].TransactionId,
                    ChunkIndex = j,
                });
                commandBuffer.AddComponent(requestRpcEneity, new SendRpcCommandRequest()
                {
                    TargetConnection = receivingFiles[i].Source
                });
                if (DebugLog) Debug.Log($"Requesting chunk {j} for file \"{receivingFiles[i].FileName}\"");
                if (requested++ >= 5) break;
            }

            if (requested > 0)
            {
                receivingFiles[i] = new BufferedReceivingFile(
                    receivingFiles[i].Source,
                    receivingFiles[i].TransactionId,
                    receivingFiles[i].FileName,
                    receivingFiles[i].TotalLength,
                    SystemAPI.Time.ElapsedTime
                );
            }
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
