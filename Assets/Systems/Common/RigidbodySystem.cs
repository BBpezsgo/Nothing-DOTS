using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct RigidbodySystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (rigidbody, collider, transform) in
            SystemAPI.Query<RefRW<Rigidbody>, RefRO<Collider>, RefRW<LocalTransform>>())
        {
            if (!rigidbody.ValueRO.IsEnabled) continue;

            switch (collider.ValueRO.Type)
            {
                case ColliderType.Sphere:
                    {
                        ref readonly SphereCollider sphere = ref collider.ValueRO.Sphere;

                        if (transform.ValueRW.Position.y - sphere.Radius <= 0.05f &&
                            math.lengthsq(rigidbody.ValueRW.Velocity) < 0.2f * 0.2f)
                        {
                            transform.ValueRW.Position.y = sphere.Radius;
                            continue;
                        }

                        transform.ValueRW.Position += rigidbody.ValueRO.Velocity * SystemAPI.Time.DeltaTime;

                        if (transform.ValueRW.Position.y - sphere.Radius <= 0f)
                        {
                            transform.ValueRW.Position.y = sphere.Radius;
                            rigidbody.ValueRW.Velocity.y = math.abs(rigidbody.ValueRW.Velocity.y);
                            rigidbody.ValueRW.Velocity *= 0.7f;
                        }
                        else
                        {
                            rigidbody.ValueRW.Velocity += new float3(0f, ProjectileSystemServer.Gravity, 0f) * SystemAPI.Time.DeltaTime;
                            rigidbody.ValueRW.Velocity -= rigidbody.ValueRW.Velocity * (0.01f * SystemAPI.Time.DeltaTime);
                        }

                        break;
                    }
                case ColliderType.AABB:
                    {
                        ref readonly AABBCollider aabb = ref collider.ValueRO.AABB;
                        float height = aabb.AABB.Extents.y;

                        if (transform.ValueRW.Position.y - height <= 0.05f &&
                            math.lengthsq(rigidbody.ValueRW.Velocity) < 0.2f * 0.2f)
                        {
                            transform.ValueRW.Position.y = height;
                            continue;
                        }

                        transform.ValueRW.Position += rigidbody.ValueRO.Velocity * SystemAPI.Time.DeltaTime;

                        if (transform.ValueRW.Position.y - height <= 0f)
                        {
                            transform.ValueRW.Position.y = height;
                            rigidbody.ValueRW.Velocity.y = math.abs(rigidbody.ValueRW.Velocity.y);
                            rigidbody.ValueRW.Velocity *= 0.7f;
                        }
                        else
                        {
                            rigidbody.ValueRW.Velocity += new float3(0f, ProjectileSystemServer.Gravity, 0f) * SystemAPI.Time.DeltaTime;
                            rigidbody.ValueRW.Velocity -= rigidbody.ValueRW.Velocity * (0.01f * SystemAPI.Time.DeltaTime);
                        }

                        break;
                    }
            }
        }
    }
}
