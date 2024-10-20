using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretRotationSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<LocalTransform> transform, RefRO<Turret> turret) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<Turret>>())
        {
            const float speed = 45f;
            quaternion target = quaternion.EulerXYZ(
                turret.ValueRO.TargetAngle,
                turret.ValueRO.TargetRotation,
                0);
            Utils.RotateTowards(ref transform.ValueRW.Rotation, target, speed * Time.deltaTime);
        }
    }
}
