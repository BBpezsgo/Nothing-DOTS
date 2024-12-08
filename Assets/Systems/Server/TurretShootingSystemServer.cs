using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretShootingSystemServer : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ProjectileDatabase>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BufferedProjectile> projectiles = SystemAPI.GetSingletonBuffer<BufferedProjectile>(true);
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRW<Turret> turret, RefRO<LocalToWorld> localToWorld) in
                    SystemAPI.Query<RefRW<Turret>, RefRO<LocalToWorld>>())
        {
            if (!turret.ValueRO.ShootRequested) continue;
            if (turret.ValueRO.Type != TurretType.Combat) continue;

            turret.ValueRW.ShootRequested = false;
            Entity instance = commandBuffer.Instantiate(turret.ValueRO.ProjectilePrefab);
            commandBuffer.SetComponent(instance, LocalTransform.FromPosition(SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position));
            commandBuffer.SetComponent(instance, new Projectile
            {
                Velocity = math.normalize(localToWorld.ValueRO.Up) * Projectile.Speed,
            });

            int projectileIndex = -1;
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i].Prefab == turret.ValueRO.ProjectilePrefab)
                {
                    projectileIndex = i;
                    break;
                }
            }

            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(request, new ShootRpc()
            {
                Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                Velocity = math.normalize(localToWorld.ValueRO.Up) * Projectile.Speed,
                ProjectileIndex = projectileIndex,
            });
            commandBuffer.AddComponent<SendRpcCommandRequest>(request);
        }
    }
}
