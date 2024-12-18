using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

#pragma warning disable CS0162 // Unreachable code detected

partial struct BufferedFileReceiverSystem : ISystem
{
    const bool DebugLog = false;
    const int ChunkRequestsLimit = 10;
    const double ChunkRequestsCooldown = 1d;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BufferedFiles>();
    }

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        DynamicBuffer<BufferedReceivingFileChunk> fileChunks = SystemAPI.GetSingletonBuffer<BufferedReceivingFileChunk>();
        DynamicBuffer<BufferedReceivingFile> receivingFiles = SystemAPI.GetSingletonBuffer<BufferedReceivingFile>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderResponseRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!state.World.IsServer()) ep = NetcodeEndPoint.Server;

            bool added = false;
            BufferedReceivingFile fileHeader = new()
            {
                Kind = command.ValueRO.Status,
                Source = ep,
                TransactionId = command.ValueRO.TransactionId,
                FileName = command.ValueRO.FileName,
                TotalLength = command.ValueRO.TotalLength,
                LastRedeivedAt = SystemAPI.Time.ElapsedTime,
                Version = command.ValueRO.Version,
            };

            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].Source != ep) continue;
                if (receivingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;

                receivingFiles[i] = fileHeader;
                added = true;
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\" from {fileHeader.Source} (again)");

                break;
            }

            if (!added)
            {
                if (DebugLog) Debug.Log($"Received file header \"{fileHeader.FileName}\" from {fileHeader.Source}");
                receivingFiles.Add(fileHeader);
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileChunkResponseRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!state.World.IsServer()) ep = NetcodeEndPoint.Server;

            bool fileFound = false;
            for (int i = 0; i < receivingFiles.Length; i++)
            {
                if (receivingFiles[i].Source != ep) continue;
                if (receivingFiles[i].TransactionId != command.ValueRO.TransactionId) continue;

                receivingFiles[i] = receivingFiles[i] with
                {
                    LastRedeivedAt = SystemAPI.Time.ElapsedTime
                };
                fileFound = true;
                if (DebugLog) Debug.Log($"{receivingFiles[i].FileName} {command.ValueRO.ChunkIndex}/{FileChunkManagerSystem.GetChunkLength(receivingFiles[i].TotalLength)}");

                break;
            }

            if (!fileFound)
            {
                Debug.LogWarning("Unexpected file chunk");
                continue;
            }

            bool added = false;
            BufferedReceivingFileChunk fileChunk = new()
            {
                Source = ep,
                TransactionId = command.ValueRO.TransactionId,
                ChunkIndex = command.ValueRO.ChunkIndex,
                Data = command.ValueRO.Data,
            };

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
        }

        int requested = 0;
        for (int i = 0; i < receivingFiles.Length; i++)
        {
            double delta = SystemAPI.Time.ElapsedTime - receivingFiles[i].LastRedeivedAt;
            if (delta < ChunkRequestsCooldown) continue;
            if (receivingFiles[i].Kind != FileResponseStatus.OK) continue;

            NativeArray<bool> receivedChunks = new(FileChunkManagerSystem.GetChunkLength(receivingFiles[i].TotalLength), Allocator.Temp);

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
                    TargetConnection = receivingFiles[i].Source.GetEntity(ref state),
                });
                if (DebugLog) Debug.Log($"Requesting chunk {j} for file \"{receivingFiles[i].FileName}\"");
                if (++requested >= ChunkRequestsLimit) break;
            }

            receivedChunks.Dispose();

            if (requested == 0) continue;

            receivingFiles[i] = receivingFiles[i] with
            {
                LastRedeivedAt = SystemAPI.Time.ElapsedTime
            };
        }
    }
}
