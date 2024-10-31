using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
partial struct ProjectileSystemClient : ISystem
{
    BufferLookup<BufferedDamage> damageQ;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        damageQ = state.GetBufferLookup<BufferedDamage>(true);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        damageQ.Update(ref state);

        using EntityCommandBuffer entityCommandBuffer = new(Unity.Collections.Allocator.Temp);
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        foreach (var (transform, worldTransform, projectile, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<LocalToWorld>, RefRW<Projectile>>()
            .WithEntityAccess())
        {
            float3 lastPosition = worldTransform.ValueRO.Position;
            transform.ValueRW.Position += projectile.ValueRO.Velocity * SystemAPI.Time.DeltaTime;
            projectile.ValueRW.Velocity += new float3(0f, ProjectileSystemServer.Gravity, 0f) * SystemAPI.Time.DeltaTime;

            if (transform.ValueRO.Position.y < 0f)
            {
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }

            RaycastInput input = new()
            {
                Start = lastPosition,
                End = worldTransform.ValueRO.Position,
                Filter = new CollisionFilter()
                {
                    BelongsTo = Layers.All,
                    CollidesWith = Layers.All,
                    GroupIndex = 0,
                },
            };

            if (!collisionWorld.CastRay(input, out RaycastHit hit))
            { continue; }

            if (damageQ.TryGetBuffer(hit.Entity, out var damage))
            {
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }
        }

        entityCommandBuffer.Playback(state.EntityManager);

        // EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        // ProjectileJob projectileJob = new()
        // {
        //     EntityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged),
        //     DeltaTime = SystemAPI.Time.DeltaTime,
        //     CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
        //     DamageQ = damageQ,
        // };

        // projectileJob.Schedule();
    }
}

/*
[BurstCompile]
public partial struct ProjectileJob : IJobEntity
{
    public EntityCommandBuffer EntityCommandBuffer;
    public float DeltaTime;
    public CollisionWorld CollisionWorld;
    public BufferLookup<BufferedDamage> DamageQ;

    [BurstCompile]
    void Execute(Entity entity, ref Projectile projectile, ref LocalTransform transform)
    {
        float3 lastPosition = transform.Position;
        transform.Position += projectile.Velocity * DeltaTime;
        projectile.Velocity += new float3(0f, -9.82f, 0f) * DeltaTime;

        if (transform.Position.y < 0f)
        {
            EntityCommandBuffer.DestroyEntity(entity);
            return;
        }

        float3 lastPositionWorld = transform.TransformPoint(lastPosition);
        float3 positionWorld = transform.TransformPoint(transform.Position);

        RaycastInput input = new()
        {
            Start = lastPositionWorld,
            End = positionWorld,
            Filter = new CollisionFilter()
            {
                BelongsTo = Layers.All,
                CollidesWith = Layers.All,
                GroupIndex = 0,
            },
        };

        if (!CollisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        { return; }

        Debug.Log("Bruh");

        if (DamageQ.TryGetBuffer(hit.Entity, out var damage))
        {
            damage.Add(new BufferedDamage(1f, math.normalize(projectile.Velocity)));
            EntityCommandBuffer.DestroyEntity(entity);
            return;
        }
    }
}
*/
