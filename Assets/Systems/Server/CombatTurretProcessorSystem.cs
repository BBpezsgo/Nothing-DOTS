using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using System;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct CombatTurretProcessorSystem : ISystem
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

        foreach (var (processor, turret) in
            SystemAPI.Query<RefRW<Processor>, RefRW<CombatTurret>>())
        {
            ref MappedMemory mapped = ref processor.ValueRW.Memory.MappedMemory;

            RefRW<LocalTransform> turretTransform = SystemAPI.GetComponentRW<LocalTransform>(turret.ValueRO.Turret);
            RefRW<LocalTransform> cannonTransform = SystemAPI.GetComponentRW<LocalTransform>(turret.ValueRO.Cannon);

            turretTransform.ValueRO.Rotation.ToEuler(out float3 turretEuler);
            cannonTransform.ValueRO.Rotation.ToEuler(out float3 cannonEuler);

            mapped.CombatTurret.TurretCurrentRotation = turretEuler.y;
            mapped.CombatTurret.TurretCurrentAngle = cannonEuler.x;

            if (float.IsFinite(mapped.CombatTurret.TurretTargetRotation))
            {
                float y = Utils.MoveTowardsAngle(turretEuler.y, mapped.CombatTurret.TurretTargetRotation, turret.ValueRO.TurretRotationSpeed * SystemAPI.Time.DeltaTime);
                turretTransform.ValueRW.Rotation = quaternion.EulerXYZ(0, y, 0);
            }

            if (float.IsFinite(mapped.CombatTurret.TurretTargetAngle))
            {
                float x = Utils.MoveTowardsAngle(cannonEuler.x, Math.Clamp(mapped.CombatTurret.TurretTargetAngle, turret.ValueRO.MinAngle, turret.ValueRO.MaxAngle), turret.ValueRO.CannonRotationSpeed * SystemAPI.Time.DeltaTime);
                cannonTransform.ValueRW.Rotation = quaternion.EulerXYZ(x, 0, 0);
            }

            if (mapped.CombatTurret.InputShoot != 0)
            {
                int projectileIndex = turret.ValueRO.Projectile;

                if (projectileIndex == -1) continue;

                mapped.CombatTurret.InputShoot = 0;

                RefRO<LocalToWorld> shootPosition = SystemAPI.GetComponentRO<LocalToWorld>(turret.ValueRO.ShootPosition);

                float3 velocity = math.normalize(shootPosition.ValueRO.Forward) * projectiles[projectileIndex].Speed;

                Entity instance = commandBuffer.Instantiate(projectiles[turret.ValueRO.Projectile].Prefab);
                commandBuffer.SetComponent(instance, LocalTransform.FromPosition(SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position));
                commandBuffer.SetComponent<Projectile>(instance, new()
                {
                    Velocity = velocity,
                    Damage = projectiles[projectileIndex].Damage,
                    ImpactEffect = projectiles[projectileIndex].ImpactEffect,
                });

                if (turret.ValueRO.ShootEffect != -1)
                {
                    Entity request = commandBuffer.CreateEntity(shootRpcArchetype);
                    commandBuffer.SetComponent<ShootRpc>(request, new()
                    {
                        Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                        Velocity = velocity,
                        ProjectileIndex = projectileIndex,
                        VisualEffectIndex = turret.ValueRO.ShootEffect,
                    });
                }
            }
        }
    }
}
