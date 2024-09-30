using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct TurretShootingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<Turret> turret, RefRO<LocalToWorld> localToWorld) in
                    SystemAPI.Query<RefRW<Turret>, RefRO<LocalToWorld>>())
        {
            if (!turret.ValueRO.ShootRequested) continue;
            turret.ValueRW.ShootRequested = false;
            Entity instance = state.EntityManager.Instantiate(turret.ValueRO.ProjectilePrefab);
            state.EntityManager.SetComponentData(instance, new LocalTransform
            {
                Position = SystemAPI.GetComponent<LocalToWorld>(turret.ValueRO.ShootPosition).Position,
                Rotation = quaternion.identity,
                Scale = SystemAPI.GetComponent<LocalTransform>(turret.ValueRO.ProjectilePrefab).Scale
            });
            state.EntityManager.SetComponentData(instance, new Projectile
            {
                Velocity = localToWorld.ValueRO.Up * 20.0f
            });
        }
    }
}
