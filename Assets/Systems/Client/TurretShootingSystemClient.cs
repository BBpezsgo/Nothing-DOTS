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

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<ShootRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            Entity projectilePrefab = projectiles[command.ValueRO.ProjectileIndex].Prefab;
            Entity projectileInstance = commandBuffer.Instantiate(projectilePrefab);
            commandBuffer.SetComponent(projectileInstance, new LocalTransform
            {
                Position = command.ValueRO.Position,
                Rotation = quaternion.LookRotation(math.normalizesafe(command.ValueRO.Velocity), new float3(0f, 1f, 0f)),
                Scale = SystemAPI.GetComponent<LocalTransform>(projectilePrefab).Scale
            });
            commandBuffer.SetComponent(projectileInstance, new Projectile
            {
                Velocity = command.ValueRO.Velocity,
                Damage = projectiles[command.ValueRO.ProjectileIndex].Damage,
            });

            if (command.ValueRO.VisualEffectIndex >= 0)
            {
                Entity visualEffectSpawn = commandBuffer.CreateEntity(visualEffectSpawnArchetype);
                commandBuffer.SetComponent(visualEffectSpawn, new VisualEffectSpawn
                {
                    Position = command.ValueRO.Position,
                    Rotation = quaternion.LookRotation(math.normalizesafe(command.ValueRO.Velocity), new float3(0f, 1f, 0f)),
                    Index = command.ValueRO.VisualEffectIndex,
                });
            }
        }
    }
}
