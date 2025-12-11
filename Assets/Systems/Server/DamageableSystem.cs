using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct DamageableSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (damageable, damages, transform, entity) in
            SystemAPI.Query<RefRW<Damageable>, DynamicBuffer<BufferedDamage>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            for (int i = 0; i < damages.Length; i++)
            {
                damageable.ValueRW.Health -= damages[i].Amount;
                if (damageable.ValueRW.Health <= 0f)
                {
                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                    NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new VisualEffectRpc()
                    {
                        Position = transform.ValueRO.Position,
                        Rotation = default,
                        Index = damageable.ValueRO.DestroyEffect,
                    });
                    commandBuffer.DestroyEntity(entity);
                    break;
                }
            }
            damages.Clear();
        }
    }
}
