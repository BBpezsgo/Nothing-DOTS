using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct TurretShootingSystemThinClient : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (_, _, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ShootRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
        }
    }
}
