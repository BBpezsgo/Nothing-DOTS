using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct BuildingSystem : ISystem
{
    public static Entity PlaceBuilding(
        EntityCommandBuffer commandBuffer,
        BufferedBuilding building,
        float3 position)
    {
        Entity newEntity = commandBuffer.Instantiate(building.PlaceholderPrefab);
        commandBuffer.SetComponent<LocalTransform>(newEntity, new()
        {
            Position = position,
            Rotation = quaternion.identity,
            Scale = 1f,
        });
        commandBuffer.SetComponent<BuildingPlaceholder>(newEntity, new()
        {
            BuildingPrefab = building.Prefab,
            CurrentProgress = 0f,
            TotalProgress = building.TotalProgress,
        });
        return newEntity;
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PlaceBuildingRequestRpc>>()
            .WithEntityAccess())
        {
            Entity buildingDatabaseEntity = SystemAPI.GetSingletonEntity<BuildingDatabase>();
            DynamicBuffer<BufferedBuilding> buildingDatabase = SystemAPI.GetBuffer<BufferedBuilding>(buildingDatabaseEntity);
            BufferedBuilding building = buildingDatabase.FirstOrDefault(static (v, c) => v.Name == c, command.ValueRO.BuildingName);

            Entity newEntity = BuildingSystem.PlaceBuilding(commandBuffer, building, command.ValueRO.Position);

            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);
            commandBuffer.SetComponent<GhostOwner>(newEntity, new()
            {
                NetworkId = networkId.ValueRO.Value,
            });

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (placeholder, transform, owner, entity) in
            SystemAPI.Query<RefRW<BuildingPlaceholder>, RefRO<LocalToWorld>, RefRO<GhostOwner>>()
            .WithEntityAccess())
        {
            if (placeholder.ValueRO.CurrentProgress >= placeholder.ValueRO.TotalProgress)
            {
                Entity newEntity = commandBuffer.Instantiate(placeholder.ValueRO.BuildingPrefab);
                commandBuffer.SetComponent<LocalTransform>(newEntity, new()
                {
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation,
                    Scale = 1f,
                });
                commandBuffer.SetComponent<GhostOwner>(newEntity, new()
                {
                    NetworkId = owner.ValueRO.NetworkId,
                });

                commandBuffer.DestroyEntity(entity);
                continue;
            }

            placeholder.ValueRW.CurrentProgress += SystemAPI.Time.DeltaTime;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
