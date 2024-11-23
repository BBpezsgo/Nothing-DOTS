using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
partial struct CompilerSystemThinClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilerStatusRpc>>()
            .WithEntityAccess())
        {
            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilationAnalysticsRpc>>()
            .WithEntityAccess())
        {
            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }
}
