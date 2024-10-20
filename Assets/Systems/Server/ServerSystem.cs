using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

#nullable enable

struct InitializedClient : IComponentData
{

}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ServerSystem : ISystem
{
    ComponentLookup<NetworkId> _clients;

    void ISystem.OnCreate(ref Unity.Entities.SystemState state)
    {
        _clients = state.GetComponentLookup<NetworkId>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        _clients.Update(ref state);

        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<PlaceBuildingRequestRpc>>()
            .WithEntityAccess())
        {
            Entity buildingDatabaseEntity = SystemAPI.GetSingletonEntity<BuildingDatabase>();
            DynamicBuffer<BufferedBuilding> buildingDatabase = SystemAPI.GetBuffer<BufferedBuilding>(buildingDatabaseEntity);
            BufferedBuilding building = buildingDatabase.FirstOrDefault(v => v.Name == command.ValueRO.BuildingName);

            Entity newEntity = commandBuffer.Instantiate(building.Prefab);
            commandBuffer.SetComponent(newEntity, new LocalTransform
            {
                Position = command.ValueRO.Position,
                Rotation = quaternion.identity,
                Scale = 1f,
            });

            NetworkId networkId = _clients[request.ValueRO.SourceConnection];
            commandBuffer.SetComponent(newEntity, new GhostOwner()
            {
                NetworkId = networkId.Value,
            });

            commandBuffer.DestroyEntity(entity);
        }

        foreach (var (id, entity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<InitializedClient>()
            .WithEntityAccess())
        {
            commandBuffer.AddComponent<InitializedClient>(entity);
            // Debug.Log($"Client #{id.ValueRO.Value} connected");
        }
        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
