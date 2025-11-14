using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
partial struct DisposeRpcSystemLocal : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (command, entity) in
            SystemAPI.Query<RefRW<ReceiveRpcCommandRequest>>()
            .WithEntityAccess())
        {
            if (command.ValueRO.Age >= 4)
            {
                commandBuffer.DestroyEntity(entity);
                continue;
            }
            command.ValueRW.Age++;
        }
    }
}
