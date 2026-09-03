using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.NetCode;

partial class RpcStreamManagerSystem : SystemBase
{
    const bool DebugLog = true;

    public readonly List<RpcStream> Streams = new();

    public static RpcStreamManagerSystem GetInstance(World world)
        => world.GetExistingSystemManaged<RpcStreamManagerSystem>();

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CreateRpcStreamRequestRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!World.IsServer()) ep = NetcodeEndPoint.Server;

            var stream = Streams.FirstOrDefault(v => v.RemoteIdentifier.Name == command.ValueRO.FileName && v.RemoteIdentifier.Source == ep);

            if (stream is null)
            {
                int transactionId = RandomManaged.Shared.Next();

                NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CreateRpcStreamResponseRpc()
                {
                    Status = FileResponseStatus.OK,
                    FileName = command.ValueRO.FileName,
                    TransactionId = transactionId,
                });

                Streams.Add(stream = new RpcStream(new FileId(command.ValueRO.FileName, ep), transactionId));
            }
            else
            {
                NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CreateRpcStreamResponseRpc()
                {
                    Status = FileResponseStatus.OK,
                    FileName = command.ValueRO.FileName,
                    TransactionId = stream.TransactionId,
                });
            }

            if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [T#{stream.TransactionId}] Sending stream header \"{command.ValueRO.FileName}\"");
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CloseStreamRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!World.IsServer()) ep = NetcodeEndPoint.Server;

            for (int i = Streams.Count - 1; i >= 0; i--)
            {
                if (Streams[i].RemoteIdentifier.Source != ep) continue;
                if (Streams[i].RemoteIdentifier.Name != command.ValueRO.FileName) continue;
                if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [T#{Streams[i].TransactionId}] Closing stream \"{command.ValueRO.FileName}\"");

                Streams[i].Complete();
                Streams.RemoveAt(i);
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CloseTransactionRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!World.IsServer()) ep = NetcodeEndPoint.Server;

            if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [T#{command.ValueRO.TransactionId}] Closing transaction `{command.ValueRO.TransactionId}`");

            for (int i = Streams.Count - 1; i >= 0; i--)
            {
                if (Streams[i].RemoteIdentifier.Source != ep) continue;
                if (Streams[i].TransactionId != command.ValueRO.TransactionId) continue;

                Streams[i].Complete();
                Streams.RemoveAt(i);
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CreateRpcStreamResponseRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!World.IsServer()) ep = NetcodeEndPoint.Server;

            var stream = Streams.FirstOrDefault(v => v.RemoteIdentifier.Name == command.ValueRO.FileName && v.RemoteIdentifier.Source == ep);

            if (stream is null)
            {
                Streams.Add(stream = new RpcStream(new FileId(command.ValueRO.FileName, ep), command.ValueRO.TransactionId));
                if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [T#{command.ValueRO.TransactionId}] Received stream header \"{command.ValueRO.FileName}\" from {ep}");
            }
            else
            {
                if (stream.TransactionId != default) Debug.LogWarning($"{DebugEx.Prefix(World.Unmanaged)} [T#{stream.TransactionId}] Received stream header \"{command.ValueRO.FileName}\" from {ep} (again, replacing transaction id with {command.ValueRO.TransactionId})");
                stream.TransactionId = command.ValueRO.TransactionId;
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRW<StreamChunkRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            NetcodeEndPoint ep = new(request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
            if (!World.IsServer()) ep = NetcodeEndPoint.Server;

            RpcStream? stream = Streams.FirstOrDefault(v => v.TransactionId == command.ValueRO.TransactionId && v.RemoteIdentifier.Source == ep);

            if (stream is null)
            {
                Debug.LogError($"{DebugEx.Prefix(World.Unmanaged)} [T#{command.ValueRO.TransactionId}] Received chunk for nonexisting stream");
            }
            else
            {
                unsafe
                {
                    int expectedIndex = stream.ReceivingIndex++;
                    if (expectedIndex != command.ValueRO.ChunkIndex)
                    {
                        Debug.LogError($"{DebugEx.Prefix(World.Unmanaged)} [T#{command.ValueRO.TransactionId}] Desync (expected {expectedIndex} got {command.ValueRO.ChunkIndex})");
                        stream.ReceivingIndex = command.ValueRO.ChunkIndex + 1;
                    }
                    else
                    {
                        if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [T#{command.ValueRO.TransactionId}] Received chunk {command.ValueRO.ChunkIndex}, next must be {stream.ReceivingIndex}");
                    }
                    stream.FeedReceived(new Span<byte>(Unsafe.AsPointer(ref command.ValueRW.Data), command.ValueRO.ChunkSize).ToArray());
                }
            }
        }

        for (int i = 0; i < Streams.Count; i++)
        {
            RpcStream stream = Streams[i];

            HandleStream(
                ref commandBuffer,
                stream,
                out bool shouldDelete
            );

            if (shouldDelete)
            {
                CloseStream(
                    ref commandBuffer,
                    stream
                );
                Streams.RemoveAt(i--);
            }
        }
    }

    protected override void OnDestroy()
    {
        Reset();
    }

    void Reset()
    {
        Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} Cancelling RPC streams");
        foreach (RpcStream item in Streams)
        {
            item.Complete();
        }

        Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} Disposing remote files");
        Streams.Clear();
    }

    void HandleStream(
        ref EntityCommandBuffer commandBuffer,
        RpcStream stream,
        out bool shouldDelete)
    {
        shouldDelete = false;

        if (stream.TransactionId == 0)
        {
            if (SystemAPI.Time.ElapsedTime - stream.RequestSentAt > 5d)
            {
                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

                Entity connection = stream.RemoteIdentifier.Source.GetEntity(World.EntityManager);

                if (connection != Entity.Null && !SystemAPI.Exists(connection))
                {
                    Debug.LogWarning($"{DebugEx.Prefix(World.Unmanaged)} [{nameof(FileChunkManagerSystem)}] Cannot send request for stream \"{stream.RemoteIdentifier.ToUri()}\": remote disconnected");
                    shouldDelete = true;
                    stream.Complete(new Exception($"Remote disconnected"));
                    return;
                }

                NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CreateRpcStreamRequestRpc()
                {
                    FileName = stream.RemoteIdentifier.Name,
                }, connection);

                stream.RequestSentAt = SystemAPI.Time.ElapsedTime;
                if (DebugLog) Debug.Log($"{DebugEx.Prefix(World.Unmanaged)} [{nameof(FileChunkManagerSystem)}] Sending request for stream \"{stream.RemoteIdentifier.ToUri()}\"");
            }

            return;
        }

        Span<byte> buffer = new byte[StreamChunkRpc.MaxChunkSize];
        buffer = buffer[..stream.DrainForSend(buffer)];
        if (!buffer.IsEmpty)
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            Entity connection = stream.RemoteIdentifier.Source.GetEntity(World.EntityManager);

            if (connection != Entity.Null && !SystemAPI.Exists(connection))
            {
                Debug.LogWarning($"{DebugEx.Prefix(World.Unmanaged)} [{nameof(FileChunkManagerSystem)}] Cannot send chunk for stream \"{stream.RemoteIdentifier.ToUri()}\": remote disconnected");
                shouldDelete = true;
                stream.Complete(new Exception($"Remote disconnected"));
                return;
            }

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new StreamChunkRpc()
                    {
                        TransactionId = stream.TransactionId,
                        ChunkIndex = stream.SendingIndex++,
                        ChunkSize = buffer.Length,
                        Data = *(FileChunk*)ptr,
                    }, connection);
                }
            }
        }
    }

    void CloseStream(
        ref EntityCommandBuffer commandBuffer,
        RpcStream stream)
    {
        if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CloseStreamRpc()
        {
            FileName = stream.RemoteIdentifier.Name,
        }, stream.RemoteIdentifier.Source.GetEntity(World.EntityManager));
        stream.Complete();
    }

    public RpcStream CreateStream(FileId remoteIdentifier)
    {
        RpcStream? res = Streams.FirstOrDefault(v => v.RemoteIdentifier == remoteIdentifier);
        if (res is not null) return res;
        res = new RpcStream(remoteIdentifier, 0);
        Streams.Add(res);
        return res;
    }

    public void OnDisconnect()
    {
        Reset();
    }
}
