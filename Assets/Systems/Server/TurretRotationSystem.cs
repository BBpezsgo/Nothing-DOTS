using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(ProcessorSystemServer))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretRotationSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<LocalTransform> transform, RefRO<Turret> turret) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<Turret>>())
        {
            const float speed = 90f;
            quaternion target = quaternion.EulerXYZ(
                turret.ValueRO.TargetAngle,
                turret.ValueRO.TargetRotation,
                0);
            Utils.RotateTowards(ref transform.ValueRW.Rotation, target, speed * SystemAPI.Time.DeltaTime);
        }
    }
}
