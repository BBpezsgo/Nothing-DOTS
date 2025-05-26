using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretShootingSystemServer : ISystem
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
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        EntityArchetype shootRpcArchetype = state.EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(ShootRpc),
            typeof(SendRpcCommandRequest),
        });

        foreach (var turret in
            SystemAPI.Query<RefRW<CombatTurret>>())
        {
            if (!turret.ValueRO.ShootRequested) continue;

            RefRO<LocalToWorld> shootPosition = SystemAPI.GetComponentRO<LocalToWorld>(turret.ValueRO.ShootPosition);

            turret.ValueRW.ShootRequested = false;

            int projectileIndex = -1;
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i].Prefab == turret.ValueRO.ProjectilePrefab)
                {
                    projectileIndex = i;
                    break;
                }
            }

            if (projectileIndex == -1) continue;

            float3 velocity = math.normalize(shootPosition.ValueRO.Forward) * projectiles[projectileIndex].Speed;

            Entity instance = commandBuffer.Instantiate(turret.ValueRO.ProjectilePrefab);
            commandBuffer.SetComponent(instance, LocalTransform.FromPosition(SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position));
            commandBuffer.SetComponent<Projectile>(instance, new()
            {
                Velocity = velocity,
                Damage = projectiles[projectileIndex].Damage,
            });

            Entity request = commandBuffer.CreateEntity(shootRpcArchetype);
            commandBuffer.SetComponent(request, new ShootRpc()
            {
                Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                Velocity = velocity,
                ProjectileIndex = projectileIndex,
                VisualEffectIndex = 0,
            });
        }
    }
}
