using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct PingSystemServer : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PingRequestRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            RefRO<NetworkId> requestId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            if (command.ValueRO.Target == 0)
            {
                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<PingResponseFinalRpc>(response, new()
                {
                    Tick = command.ValueRO.Tick,
                    Source = 0,
                });
            }
            else
            {
                foreach (var (networkId, connection) in
                    SystemAPI.Query<RefRO<NetworkId>>()
                    .WithEntityAccess())
                {
                    if (networkId.ValueRO.Value != command.ValueRO.Target) continue;
                    Entity response = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                    {
                        TargetConnection = connection,
                    });
                    commandBuffer.AddComponent<PingRequestForwardRpc>(response, new()
                    {
                        Source = (byte)requestId.ValueRO.Value,
                        Tick = command.ValueRO.Tick,
                    });
                    break;
                }
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PingResponseForwardRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            if (command.ValueRO.Target == 0) continue;

            RefRO<NetworkId> sourceNetworkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            foreach (var (networkId, connection) in
                SystemAPI.Query<RefRO<NetworkId>>()
                .WithEntityAccess())
            {
                if (networkId.ValueRO.Value != command.ValueRO.Target) continue;

                Entity forward = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(forward, new()
                {
                    TargetConnection = connection,
                });
                commandBuffer.AddComponent<PingResponseFinalRpc>(forward, new()
                {
                    Tick = command.ValueRO.Tick,
                    Source = (byte)sourceNetworkId.ValueRO.Value,
                });

                break;
            }
        }
    }
}
