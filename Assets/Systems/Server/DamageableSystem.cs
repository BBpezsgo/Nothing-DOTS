using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct DamageableSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (damageable, damages, entity) in
            SystemAPI.Query<RefRW<Damageable>, DynamicBuffer<BufferedDamage>>()
            .WithEntityAccess())
        {
            for (int i = damages.Length - 1; i >= 0; i--)
            {
                if ((damageable.ValueRW.Health -= damages[i].Amount) <= 0f)
                {
                    commandBuffer.DestroyEntity(entity);
                }
            }
            damages.Clear();
        }
    }
}
