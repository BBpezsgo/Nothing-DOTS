using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretRotationSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<LocalTransform> transform, RefRO<Turret> turret) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<Turret>>())
        {
            const float speed = math.PI;
            Utils.QuaternionToEuler(transform.ValueRO.Rotation, out float3 euler);
            float x = Utils.MoveTowardsAngle(euler.x, turret.ValueRO.TargetAngle, speed * SystemAPI.Time.DeltaTime);
            float y = Utils.MoveTowardsAngle(euler.y, turret.ValueRO.TargetRotation, speed * SystemAPI.Time.DeltaTime);
            transform.ValueRW.Rotation = quaternion.EulerXYZ(x, y, 0);
        }
    }
}
