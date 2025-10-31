using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct BuildingSystemServer : ISystem
{
    public static Entity PlaceBuilding(
        EntityCommandBuffer commandBuffer,
        BufferedBuilding building,
        float3 position)
    {
        Entity newEntity = commandBuffer.Instantiate(building.PlaceholderPrefab);
        commandBuffer.SetComponent<LocalTransform>(newEntity, LocalTransform.FromPosition(position));
        commandBuffer.SetComponent<BuildingPlaceholder>(newEntity, new()
        {
            BuildingPrefab = building.Prefab,
            CurrentProgress = 0f,
            TotalProgress = building.ConstructionTime,
        });
        return newEntity;
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PlaceBuildingRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            (Entity Entity, Player Player) requestPlayer = default;

            foreach (var (player, _entity) in
                SystemAPI.Query<RefRO<Player>>()
                .WithEntityAccess())
            {
                if (player.ValueRO.ConnectionId != networkId.ValueRO.Value) continue;
                requestPlayer = (_entity, player.ValueRO);
                break;
            }

            if (requestPlayer.Entity == Entity.Null)
            {
                Debug.LogError(string.Format("Failed to place building: requested by {0} but aint have a team", networkId.ValueRO));
                continue;
            }

            DynamicBuffer<BufferedAcquiredResearch> acquiredResearches = SystemAPI.GetBuffer<BufferedAcquiredResearch>(requestPlayer.Entity);
            DynamicBuffer<BufferedBuilding> buildings = SystemAPI.GetBuffer<BufferedBuilding>(SystemAPI.GetSingletonEntity<BuildingDatabase>());

            BufferedBuilding building = default;

            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Name == command.ValueRO.BuildingName)
                {
                    building = buildings[i];
                    break;
                }
            }

            if (building.Prefab == Entity.Null)
            {
                Debug.LogWarning($"Building \"{command.ValueRO.BuildingName}\" not found in the database");
                continue;
            }

            if (!building.RequiredResearch.IsEmpty)
            {
                bool can = false;
                foreach (var research in acquiredResearches)
                {
                    if (research.Name != building.RequiredResearch) continue;
                    can = true;
                    break;
                }

                if (!can)
                {
                    Debug.LogWarning($"Can't place building \"{building.Name}\": not researched");
                    continue;
                }
            }

            if (requestPlayer.Player.Resources < building.RequiredResources)
            {
                Debug.LogWarning($"Can't place building \"{building.Name}\": not enought resources ({requestPlayer.Player.Resources} < {building.RequiredResources})");
                break;
            }

            foreach (var _player in
                SystemAPI.Query<RefRW<Player>>())
            {
                if (_player.ValueRO.ConnectionId != networkId.ValueRO.Value) continue;
                _player.ValueRW.Resources -= building.RequiredResources;
                break;
            }

            Entity newEntity = BuildingSystemServer.PlaceBuilding(commandBuffer, building, command.ValueRO.Position);
            commandBuffer.SetComponent<UnitTeam>(newEntity, new()
            {
                Team = requestPlayer.Player.Team,
            });
            commandBuffer.SetComponent<GhostOwner>(newEntity, new()
            {
                NetworkId = networkId.ValueRO.Value,
            });
        }

        foreach (var (placeholder, transform, owner, unitTeam, entity) in
            SystemAPI.Query<RefRW<BuildingPlaceholder>, RefRO<LocalToWorld>, RefRO<GhostOwner>, RefRO<UnitTeam>>()
            .WithEntityAccess())
        {
            if (placeholder.ValueRO.CurrentProgress >= placeholder.ValueRO.TotalProgress)
            {
                Entity newEntity = commandBuffer.Instantiate(placeholder.ValueRO.BuildingPrefab);
                commandBuffer.SetComponent<LocalTransform>(newEntity, LocalTransform.FromPositionRotation(transform.ValueRO.Position, transform.ValueRO.Rotation));
                commandBuffer.SetComponent<GhostOwner>(newEntity, new()
                {
                    NetworkId = owner.ValueRO.NetworkId,
                });
                commandBuffer.SetComponent<UnitTeam>(newEntity, new()
                {
                    Team = unitTeam.ValueRO.Team,
                });

                commandBuffer.DestroyEntity(entity);
                continue;
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<BuildingsRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            Entity requestPlayer = default;

            foreach (var (player, _entity) in
                SystemAPI.Query<RefRO<Player>>()
                .WithEntityAccess())
            {
                if (player.ValueRO.ConnectionId != networkId.ValueRO.Value) continue;
                requestPlayer = _entity;
                break;
            }

            if (requestPlayer == Entity.Null)
            {
                Debug.LogError(string.Format("Player with network id {0} aint have a team", networkId));
                continue;
            }

            DynamicBuffer<BufferedAcquiredResearch> acquiredResearches = SystemAPI.GetBuffer<BufferedAcquiredResearch>(requestPlayer);
            DynamicBuffer<BufferedBuilding> buildings = SystemAPI.GetBuffer<BufferedBuilding>(SystemAPI.GetSingletonEntity<BuildingDatabase>());

            foreach (BufferedBuilding building in buildings)
            {
                if (!building.RequiredResearch.IsEmpty)
                {
                    bool can = false;
                    foreach (BufferedAcquiredResearch research in acquiredResearches)
                    {
                        if (research.Name != building.RequiredResearch) continue;
                        can = true;
                        break;
                    }

                    if (!can) continue;
                }

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new BuildingsResponseRpc()
                {
                    Name = building.Name,
                }, request.ValueRO.SourceConnection);
            }
        }
    }
}
