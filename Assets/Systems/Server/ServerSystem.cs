using System;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

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
        var spawns = SystemAPI.GetSingletonBuffer<BufferedSpawn>(false);

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
                IsCoreComputerSpawned = false,
                Resources = 5,
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
            else
            {
                if (!item.ValueRO.IsCoreComputerSpawned)
                {
                    for (int i = 0; i < spawns.Length; i++)
                    {
                        if (spawns[i].IsOccupied) continue;
                        spawns[i] = spawns[i] with { IsOccupied = true };

                        Entity coreComputer = commandBuffer.Instantiate(prefabs.CoreComputer);
                        commandBuffer.SetComponent<UnitTeam>(coreComputer, new()
                        {
                            Team = item.ValueRO.Team
                        });
                        commandBuffer.SetComponent<LocalTransform>(coreComputer, LocalTransform.FromPosition(spawns[i].Position));
                        commandBuffer.SetComponent<GhostOwner>(coreComputer, new()
                        {
                            NetworkId = item.ValueRO.ConnectionId,
                        });

                        Entity builder = commandBuffer.Instantiate(prefabs.Builder);
                        commandBuffer.SetComponent<UnitTeam>(builder, new()
                        {
                            Team = item.ValueRO.Team
                        });
                        commandBuffer.SetComponent<LocalTransform>(builder, LocalTransform.FromPosition(spawns[i].Position + new Unity.Mathematics.float3(2f, 0.5f, 2f)));
                        commandBuffer.SetComponent<GhostOwner>(builder, new()
                        {
                            NetworkId = item.ValueRO.ConnectionId,
                        });

                        break;
                    }
                    item.ValueRW.IsCoreComputerSpawned = true;
                }
            }
        }
    }
}
