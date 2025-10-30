using System;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct PingSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;
        long now = DateTime.UtcNow.Ticks;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PingResponseFinalRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            foreach (var player in
                SystemAPI.Query<RefRW<Player>>())
            {
                if (player.ValueRO.ConnectionId != command.ValueRO.Source) continue;

                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                if (player.ValueRO.PingRequested == command.ValueRO.Tick)
                {
                    player.ValueRW.Ping = (int)(now - command.ValueRO.Tick);
                }

                player.ValueRW.PingResponded = now;
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PingRequestRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            Entity response = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
            {
                TargetConnection = request.ValueRO.SourceConnection,
            });
            commandBuffer.AddComponent<PingResponseRpc>(response, new()
            {
                Tick = command.ValueRO.Tick,
            });
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PingRequestForwardRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            Entity response = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
            {
                TargetConnection = request.ValueRO.SourceConnection,
            });
            commandBuffer.AddComponent<PingResponseForwardRpc>(response, new()
            {
                Tick = command.ValueRO.Tick,
                Target = command.ValueRO.Source,
            });
        }

        foreach (var player in
            SystemAPI.Query<RefRW<Player>>())
        {
            if ((player.ValueRO.PingResponded == 0 || TimeSpan.FromTicks(now - player.ValueRO.PingResponded).TotalSeconds > 2) && TimeSpan.FromTicks(now - player.ValueRO.PingRequested).TotalSeconds > 2)
            {
                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response);
                commandBuffer.AddComponent<PingRequestRpc>(response, new()
                {
                    Target = (byte)player.ValueRO.ConnectionId,
                    Tick = now,
                });
                player.ValueRW.PingRequested = now;
            }
        }
    }
}
