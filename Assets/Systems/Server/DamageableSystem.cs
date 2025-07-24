using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct DamageableSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        EntityArchetype visualEffectRpcArchetype = state.EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(VisualEffectRpc),
            typeof(SendRpcCommandRequest ),
        });

        foreach (var (damageable, damages, transform, entity) in
            SystemAPI.Query<RefRW<Damageable>, DynamicBuffer<BufferedDamage>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            for (int i = damages.Length - 1; i >= 0; i--)
            {
                if ((damageable.ValueRW.Health -= damages[i].Amount) <= 0f)
                {
                    Entity request = commandBuffer.CreateEntity(visualEffectRpcArchetype);
                    commandBuffer.SetComponent<VisualEffectRpc>(request, new()
                    {
                        Position = transform.ValueRO.Position,
                        Rotation = default,
                        Index = damageable.ValueRO.DestroyEffect,
                    });
                    commandBuffer.DestroyEntity(entity);
                }
            }
            damages.Clear();
        }
    }
}
