using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct RigidbodySystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (rigidbody, collider, transform) in
            SystemAPI.Query<RefRW<Rigidbody>, RefRO<Collider>, RefRW<LocalTransform>>())
        {
            if (collider.ValueRO.Type != ColliderType.Sphere) continue;
            ref readonly SphereCollider sphere = ref collider.ValueRO.Sphere;

            if (math.lengthsq(rigidbody.ValueRW.Velocity) < 0.3f * 0.3f)
            { continue; }

            float3 direction = math.normalize(rigidbody.ValueRO.Velocity);
            float3 right = math.cross(new float3(0f, 1f, 0f), direction);
            float distance = math.length(rigidbody.ValueRO.Velocity) * SystemAPI.Time.DeltaTime;
            float alpha = distance / (sphere.Radius == 0f ? 1f : sphere.Radius);
            transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.AxisAngle(right, alpha));
        }
    }
}
