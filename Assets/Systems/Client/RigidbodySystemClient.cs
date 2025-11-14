using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct RigidbodySystemClient : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (rigidbody, collider, transform) in
            SystemAPI.Query<RefRW<Rigidbody>, RefRO<Collider>, RefRW<LocalTransform>>())
        {
            switch (collider.ValueRO.Type)
            {
                case ColliderType.Sphere:
                {
                    ref readonly SphereCollider sphere = ref collider.ValueRO.Sphere;

                    if (math.lengthsq(rigidbody.ValueRW.Velocity) < 0.3f * 0.3f)
                    { continue; }

                    float3 direction = math.normalize(rigidbody.ValueRO.Velocity);
                    float3 right = math.cross(new float3(0f, 1f, 0f), direction);
                    float distance = math.length(rigidbody.ValueRO.Velocity) * SystemAPI.Time.DeltaTime;
                    float alpha = distance / (sphere.Radius == 0f ? 1f : sphere.Radius);
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.AxisAngle(right, alpha));
                    break;
                }

                case ColliderType.AABB:
                {
                    ref readonly AABBCollider aabb = ref collider.ValueRO.AABB;

                    float height = aabb.AABB.Extents.y;

                    if (transform.ValueRW.Position.y - height <= 0.05f &&
                        math.lengthsq(rigidbody.ValueRW.Velocity) < 3.0f * 3.0f)
                    {
                        transform.ValueRW.Rotation.ToEuler(out float3 euler);
                        transform.ValueRW.Rotation = math.mul(quaternion.identity, quaternion.Euler(0f, euler.y, 0f));
                        continue;
                    }

                    if (math.lengthsq(rigidbody.ValueRW.Velocity) < 0.3f * 0.3f)
                    { continue; }

                    float3 direction = math.normalize(rigidbody.ValueRO.Velocity);
                    float3 right = math.cross(new float3(0f, 1f, 0f), direction);
                    float distance = math.length(rigidbody.ValueRO.Velocity) * SystemAPI.Time.DeltaTime;
                    float size = math.length(aabb.AABB.Extents);
                    float alpha = distance / (size == 0f ? 1f : size);
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.AxisAngle(right, alpha));

                    break;
                }
            }
        }
    }
}
