using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

#pragma warning disable CS0162 // Unreachable code detected

partial struct BufferedFileSenderSystem : ISystem
{
    const bool DebugLog = false;
    const int ChunkSendingLimit = 1;
    const double ChunkSendingCooldown = 0.01d;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BufferedFiles>();
    }

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        DynamicBuffer<BufferedSentFileChunk> sentChunks = SystemAPI.GetSingletonBuffer<BufferedSentFileChunk>();
        DynamicBuffer<BufferedSendingFile> sendingFiles = SystemAPI.GetSingletonBuffer<BufferedSendingFile>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FileHeaderRequestRpc>>()
            .WithEntityAccess())
        {
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);

            FileData? localFile = FileChunkManager.GetLocalFile(command.ValueRO.FileName.ToString());
            if (!localFile.HasValue)
            {
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    Kind = FileHeaderKind.NotFound,
                    FileName = command.ValueRO.FileName,
                    TransactionId = default,
                    TotalLength = default,
                    Version = MonoTime.Ticks,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());

                if (DebugLog) Debug.LogError($"File \"{command.ValueRO.FileName}\" does not exists");
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            /*
            bool found = false;

            for (int i = 0; i < sendingFiles.Length; i++)
            {
                if (sendingFiles[i].Destination != ep) continue;
                if (sendingFiles[i].FileName != command.ValueRO.FileName) continue;

                Entity responseRpcEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    Kind = FileHeaderKind.Ok,
                    FileName = sendingFiles[i].FileName,
                    TransactionId = sendingFiles[i].TransactionId,
                    TotalLength = localFile.Value.Data.Length,
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
            */

            {
                int transactionId = Maths.Random.Integer();
                FileHeaderKind kind = command.ValueRO.Method switch
                {
                    FileRequestMethod.OnlyHeader => FileHeaderKind.OnlyHeader,
                    _ => FileHeaderKind.Ok,
                };
                Entity responseRpcEntity = commandBuffer.CreateEntity();
                int totalLength = localFile.Value.Data.Length;
                commandBuffer.AddComponent(responseRpcEntity, new FileHeaderRpc()
                {
                    Kind = kind,
                    FileName = command.ValueRO.FileName,
                    TransactionId = transactionId,
                    TotalLength = totalLength,
                    Version = localFile.Value.Version,
                });
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());
                if (command.ValueRO.Method != FileRequestMethod.OnlyHeader)
                {
                    sendingFiles.Add(new BufferedSendingFile(
                        ep,
                        transactionId,
                        command.ValueRO.FileName,
                        command.ValueRO.Method == FileRequestMethod.HeaderAndData,
                        SystemAPI.Time.ElapsedTime,
                        totalLength
                    ));
                }
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

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CloseFileRpc>>()
            .WithEntityAccess())
        {
            NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);

            for (int i = sendingFiles.Length - 1; i >= 0; i--)
            {
                if (sendingFiles[i].Destination != ep) continue;
                if (sendingFiles[i].FileName != command.ValueRO.FileName) continue;

                for (int j = sentChunks.Length - 1; j >= 0; j--)
                {
                    if (sentChunks[j].Destination != ep) continue;
                    if (sentChunks[j].TransactionId != sendingFiles[i].TransactionId) continue;

                    sentChunks.RemoveAt(j);
                }

                sendingFiles.RemoveAt(i);
            }

            commandBuffer.DestroyEntity(entity);
        }

        int sent = 0;
        NativeList<bool> currentSentChunks = default;
        for (int i = 0; i < sendingFiles.Length; i++)
        {
            if (!sendingFiles[i].AutoSendEverything) continue;
            double delta = SystemAPI.Time.ElapsedTime - sendingFiles[i].LastSentAt;
            if (delta < ChunkSendingCooldown) continue;
            if (!currentSentChunks.IsCreated) currentSentChunks = new(Allocator.Temp);
            currentSentChunks.Resize(FileChunkManager.GetChunkLength(sendingFiles[i].TotalLength), NativeArrayOptions.ClearMemory);

            for (int j = 0; j < sentChunks.Length; j++)
            {
                if (sentChunks[j].Destination != sendingFiles[i].Destination) continue;
                if (sentChunks[j].TransactionId != sendingFiles[i].TransactionId) continue;

                currentSentChunks[sentChunks[j].ChunkIndex] = true;
            }

            for (int j = 0; j < currentSentChunks.Length; j++)
            {
                if (currentSentChunks[j]) continue;

                Entity responseRpcEntity = commandBuffer.CreateEntity();
                FileData? file = FileChunkManager.GetLocalFile(sendingFiles[i].FileName.ToString());
                int chunkSize = FileChunkManager.GetChunkSize(file!.Value.Data.Length, j);
                Span<byte> buffer = file!.Value.Data.AsSpan().Slice(j * FileChunkRpc.ChunkSize, chunkSize);
                fixed (byte* bufferPtr = buffer)
                {
                    commandBuffer.AddComponent(responseRpcEntity, new FileChunkRpc
                    {
                        TransactionId = sendingFiles[i].TransactionId,
                        ChunkIndex = j,
                        Data = *(FixedBytes126*)bufferPtr,
                    });
                }
                commandBuffer.AddComponent(responseRpcEntity, new SendRpcCommandRequest());

                sentChunks.Add(new BufferedSentFileChunk(
                    sendingFiles[i].Destination,
                    sendingFiles[i].TransactionId,
                    j
                ));

                if (DebugLog) Debug.Log($"Sending chunk {j} for file {sendingFiles[i].FileName}");

                if (++sent >= ChunkSendingLimit) break;
            }
        }
        if (currentSentChunks.IsCreated) currentSentChunks.Dispose();
    }
}
