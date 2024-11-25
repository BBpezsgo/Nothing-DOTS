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
            const float speed = 180f;
            Utils.QuaternionToEuler(transform.ValueRO.Rotation, out float3 euler);
            float x = math.radians(UnityEngine.Mathf.MoveTowardsAngle(math.degrees(euler.x), math.degrees(turret.ValueRO.TargetAngle), speed * SystemAPI.Time.DeltaTime));
            float y = math.radians(UnityEngine.Mathf.MoveTowardsAngle(math.degrees(euler.y), math.degrees(turret.ValueRO.TargetRotation), speed * SystemAPI.Time.DeltaTime));
            transform.ValueRW.Rotation = quaternion.EulerXYZ(x, y, 0);
        }
    }
}
