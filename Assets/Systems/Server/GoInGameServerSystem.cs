using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GoInServerClientSystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        builder.WithAll<ReceiveRpcCommandRequest, GoInGameRpcCommand>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<GoInGameRpcCommand>>()
            .WithEntityAccess())
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(request.ValueRO.SourceConnection);
            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
