using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct BuildingSystem : ISystem
{
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

            Entity newEntity = commandBuffer.Instantiate(building.Prefab);
            commandBuffer.SetComponent(newEntity, new LocalTransform
            {
                Position = command.ValueRO.Position,
                Rotation = quaternion.identity,
                Scale = 1f,
            });

            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);
            commandBuffer.SetComponent(newEntity, new GhostOwner()
            {
                NetworkId = networkId.ValueRO.Value,
            });

            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
