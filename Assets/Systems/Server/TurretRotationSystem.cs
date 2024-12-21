using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// [UpdateInGroup(typeof(TransformSystemGroup))]
// [UpdateBefore(typeof(LocalToWorldSystem))]
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TurretRotationSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (transform, turret) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Turret>>())
        {
            RefRW<LocalTransform> cannon = SystemAPI.GetComponentRW<LocalTransform>(turret.ValueRO.Cannon);
            {
                transform.ValueRO.Rotation.ToEuler(out float3 euler);
                float y = Utils.MoveTowardsAngle(euler.y, turret.ValueRO.TargetRotation, turret.ValueRO.TurretRotationSpeed * SystemAPI.Time.DeltaTime);
                transform.ValueRW.Rotation = quaternion.EulerXYZ(0, y, 0);
            }
            {
                cannon.ValueRO.Rotation.ToEuler(out float3 euler);
                float x =
                    Utils.DeltaAngle(euler.y, turret.ValueRO.TargetRotation) < math.PI
                    ? Utils.MoveTowardsAngle(euler.x, turret.ValueRO.TargetAngle, turret.ValueRO.CannonRotationSpeed * SystemAPI.Time.DeltaTime)
                    : euler.x;
                cannon.ValueRW.Rotation = quaternion.EulerXYZ(x, 0, 0);
            }
        }

        foreach (var (transform, turret) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<BuilderTurret>>())
        {
            const float speed = math.PI;
            transform.ValueRO.Rotation.ToEuler(out float3 euler);
            // float x =
            //     Utils.DeltaAngle(euler.y, turret.ValueRO.TargetRotation) < math.PI
            //     ? Utils.MoveTowardsAngle(euler.x, turret.ValueRO.TargetAngle, speed * SystemAPI.Time.DeltaTime)
            //     : euler.x;
            float y = Utils.MoveTowardsAngle(euler.y, turret.ValueRO.TargetRotation, speed * SystemAPI.Time.DeltaTime);
            transform.ValueRW.Rotation = quaternion.EulerXYZ(math.PIHALF, y, 0f);
        }
    }
}
