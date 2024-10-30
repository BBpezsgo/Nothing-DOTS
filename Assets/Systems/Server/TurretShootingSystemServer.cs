using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretShootingSystemServer : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BufferedProjectile> projectiles = SystemAPI.GetSingletonBuffer<BufferedProjectile>(true);
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach ((RefRW<Turret> turret, RefRO<LocalToWorld> localToWorld) in
                    SystemAPI.Query<RefRW<Turret>, RefRO<LocalToWorld>>())
        {
            if (!turret.ValueRO.ShootRequested) continue;
            turret.ValueRW.ShootRequested = false;
            Entity instance = commandBuffer.Instantiate(turret.ValueRO.ProjectilePrefab);
            commandBuffer.SetComponent(instance, new LocalTransform
            {
                Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                Rotation = quaternion.identity,
                Scale = SystemAPI.GetComponent<LocalTransform>(turret.ValueRO.ProjectilePrefab).Scale
            });
            commandBuffer.SetComponent(instance, new Projectile
            {
                Velocity = math.normalize(localToWorld.ValueRO.Up) * Projectile.Speed
            });

            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(request, new ShootRpc()
            {
                Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                Velocity = math.normalize(localToWorld.ValueRO.Up) * Projectile.Speed,
                ProjectileIndex = projectiles.IndexOf(static (v, c) => v.Prefab == c, turret.ValueRO.ProjectilePrefab),
            });
            commandBuffer.AddComponent<SendRpcCommandRequest>(request);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
