using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GoInServerClientSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        builder.WithAll<ReceiveRpcCommandRequest, GoInGameRpc>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameRpc>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            commandBuffer.AddComponent<NetworkStreamInGame>(request.ValueRO.SourceConnection);
        }
    }
}
