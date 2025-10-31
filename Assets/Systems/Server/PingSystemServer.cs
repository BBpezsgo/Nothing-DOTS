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
                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new PingResponseFinalRpc()
                {
                    Tick = command.ValueRO.Tick,
                    Source = 0,
                }, request.ValueRO.SourceConnection);
            }
            else
            {
                foreach (var (networkId, connection) in
                    SystemAPI.Query<RefRO<NetworkId>>()
                    .WithEntityAccess())
                {
                    if (networkId.ValueRO.Value != command.ValueRO.Target) continue;
                    NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new PingRequestForwardRpc()
                    {
                        Source = (byte)requestId.ValueRO.Value,
                        Tick = command.ValueRO.Tick,
                    }, connection);
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

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new PingResponseFinalRpc()
                {
                    Tick = command.ValueRO.Tick,
                    Source = (byte)sourceNetworkId.ValueRO.Value,
                }, connection);

                break;
            }
        }
    }
}
