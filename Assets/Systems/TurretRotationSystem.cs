using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct TurretRotationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<LocalTransform> transform, RefRO<Turret> turret) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<Turret>>())
        {
            const float speed = 45f;
            quaternion target = quaternion.Euler(
                math.radians(turret.ValueRO.TargetAngle),
                math.radians(turret.ValueRO.TargetRotation),
                0);
            target = Utils.RotateTowards(transform.ValueRO.Rotation, target, speed * Time.deltaTime);
            transform.ValueRW.Rotation = target;
        }
    }
}
