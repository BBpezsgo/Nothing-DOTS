using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#pragma warning disable CS0162 // Unreachable code detected

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
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);

            FileData? localFile = FileChunkManager.GetLocalFile(command.ValueRO.FileName.ToString());
            if (!localFile.HasValue)
            {
                if (DebugLog) Debug.LogError($"File \"{command.ValueRO.FileName}\" does not exists");
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            bool found = false;

            for (int i = 0; i < sendingFiles.Length; i++)
            {
                if (sendingFiles[i].Destination != ep) continue;
                if (sendingFiles[i].FileName != command.ValueRO.FileName) continue;

                Entity responseRpcEntity = commandBuffer.CreateEntity();
                int totalLength = localFile.Value.Data.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = sendingFiles[i].FileName,
                    TransactionId = sendingFiles[i].TransactionId,
                    TotalLength = totalLength,
                    Version = localFile.Value.Version,
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
                int totalLength = localFile.Value.Data.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    FileName = command.ValueRO.FileName,
                    TransactionId = command.ValueRO.FileName.GetHashCode(),
                    TotalLength = totalLength,
                    Version = localFile.Value.Version,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                found = true;
                sendingFiles.Add(new BufferedSendingFile(
                    ep,
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
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);

            bool found = false;

            for (int i = 0; i < sendingFiles.Length; i++)
            {
                if (sendingFiles[i].Destination != ep) continue;
                if (sendingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;

                Entity responseRpcEntity = commandBuffer.CreateEntity();
                FileData? file = FileChunkManager.GetLocalFile(sendingFiles[i].FileName.ToString());
                int chunkSize = FileChunkManager.GetChunkSize(file!.Value.Data.Length, command.ValueRO.ChunkIndex);
                Span<byte> buffer = file!.Value.Data.AsSpan().Slice(command.ValueRO.ChunkIndex * FileChunkRpc.ChunkSize, chunkSize);
                fixed (byte* bufferPtr = buffer)
                {
                    commandBuffer.AddComponent(responseRpcEntity, new FileChunkRpc
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
