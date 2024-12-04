using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct ProjectileSystemServer : ISystem
{
    BufferLookup<BufferedDamage> damageQ;
    public const float Gravity = -9.82f;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        damageQ = state.GetBufferLookup<BufferedDamage>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        damageQ.Update(ref state);
        var map = QuadrantSystem.GetMap(ref state);

        foreach (var (transform, projectile, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<Projectile>>()
            .WithEntityAccess())
        {
            float3 lastPosition = transform.ValueRO.Position;
            float3 newPosition = lastPosition + (projectile.ValueRO.Velocity * SystemAPI.Time.DeltaTime);
            transform.ValueRW.Position = newPosition;
            projectile.ValueRW.Velocity += new float3(0f, Gravity, 0f) * SystemAPI.Time.DeltaTime;

            if (transform.ValueRO.Position.y < 0f)
            {
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }

            Ray ray = new(lastPosition, newPosition, Layers.Default);

            if (!QuadrantRayCast.RayCast(map, ray, out Hit hit))
            { continue; }

            if (damageQ.TryGetBuffer(hit.Entity.Entity, out DynamicBuffer<BufferedDamage> damage))
            {
                damage.Add(new BufferedDamage(1f, math.normalize(projectile.ValueRO.Velocity)));
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }
        }

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
