using System;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ServerSystem : ISystem
{
    int _teamCounter;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        _teamCounter = 1;
        state.RequireForUpdate<PrefabDatabase>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        PrefabDatabase prefabs = SystemAPI.GetSingleton<PrefabDatabase>();

        foreach (var (id, entity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<InitializedClient>()
            .WithEntityAccess())
        {
            commandBuffer.AddComponent<InitializedClient>(entity);

            Entity newPlayer = commandBuffer.Instantiate(prefabs.Player);
            commandBuffer.SetComponent<Player>(newPlayer, new()
            {
                ConnectionId = id.ValueRO.Value,
                ConnectionState = PlayerConnectionState.Connected,
                Team = -1,
            });
        }

        foreach (var item in
            SystemAPI.Query<RefRW<Player>>())
        {
            if (item.ValueRO.Team == -1)
            {
                item.ValueRW.Team = _teamCounter++;
            }

            if (item.ValueRO.ConnectionState != PlayerConnectionState.Connected) continue;

            bool found = false;
            foreach (var (id, _, entity) in
                SystemAPI.Query<RefRO<NetworkId>, RefRO<InitializedClient>>()
                .WithEntityAccess())
            {
                if (id.ValueRO.Value == item.ValueRO.ConnectionId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                item.ValueRW.ConnectionId = -1;
                item.ValueRW.ConnectionState = PlayerConnectionState.Disconnected;
            }
        }
    }
}
