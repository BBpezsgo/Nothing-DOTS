using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct TurretShootingSystemClient : ISystem
{
    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ProjectileDatabase>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BufferedProjectile> projectiles = SystemAPI.GetSingletonBuffer<BufferedProjectile>(true);
        EntityArchetype visualEffectSpawnArchetype = state.EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(VisualEffectSpawn),
        });

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<ShootRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            Entity projectilePrefab = projectiles[command.ValueRO.ProjectileIndex].Prefab;
            Entity projectileInstance = commandBuffer.Instantiate(projectilePrefab);
            commandBuffer.SetComponent<LocalTransform>(projectileInstance, new()
            {
                Position = command.ValueRO.Position,
                Rotation = quaternion.LookRotation(math.normalizesafe(command.ValueRO.Velocity), new float3(0f, 1f, 0f)),
                Scale = SystemAPI.GetComponent<LocalTransform>(projectilePrefab).Scale
            });
            commandBuffer.SetComponent<Projectile>(projectileInstance, new()
            {
                Velocity = command.ValueRO.Velocity,
                Damage = projectiles[command.ValueRO.ProjectileIndex].Damage,
                ImpactEffect = projectiles[command.ValueRO.ProjectileIndex].ImpactEffect,
            });

            if (command.ValueRO.VisualEffectIndex >= 0)
            {
                Entity visualEffectSpawn = commandBuffer.CreateEntity(visualEffectSpawnArchetype);
                commandBuffer.SetComponent<VisualEffectSpawn>(visualEffectSpawn, new()
                {
                    Position = command.ValueRO.Position,
                    Rotation = quaternion.LookRotation(math.normalizesafe(command.ValueRO.Velocity), new float3(0f, 1f, 0f)),
                    Index = command.ValueRO.VisualEffectIndex,
                });
            }
        }
    }
}
