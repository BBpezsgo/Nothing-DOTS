using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct ProjectileSystemClient : ISystem
{
    BufferLookup<BufferedDamage> DamageQ;
    EntityArchetype VisualEffectSpawnArchetype;

    void ISystem.OnCreate(ref SystemState state)
    {
        DamageQ = state.GetBufferLookup<BufferedDamage>(true);
        VisualEffectSpawnArchetype = state.EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(VisualEffectSpawn),
        });
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        DamageQ.Update(ref state);
        var map = QuadrantSystem.GetMap(ref state);

        foreach (var (transform, projectile, entity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<Projectile>>()
            .WithEntityAccess())
        {
            float3 lastPosition = transform.ValueRO.Position;
            float3 newPosition = lastPosition + (projectile.ValueRO.Velocity * SystemAPI.Time.DeltaTime);
            projectile.ValueRW.Velocity += new float3(0f, ProjectileSystemServer.Gravity, 0f) * SystemAPI.Time.DeltaTime;
            transform.ValueRW.Position = newPosition;
            transform.ValueRW.Rotation = quaternion.LookRotation(math.normalizesafe(projectile.ValueRO.Velocity), new float3(0f, 1f, 0f));

            if (transform.ValueRO.Position.y < 0f)
            {
                commandBuffer.DestroyEntity(entity);
                continue;
            }

            Ray ray = new(lastPosition, newPosition, Layers.BuildingOrUnit);

            if (!QuadrantRayCast.RayCast(map, ray, out var hit))
            { continue; }

            if (DamageQ.HasBuffer(hit.Entity.Entity))
            {
                if (projectile.ValueRO.ImpactEffect != -1)
                {
                    Entity visualEffectSpawn = commandBuffer.CreateEntity(VisualEffectSpawnArchetype);
                    commandBuffer.SetComponent<VisualEffectSpawn>(visualEffectSpawn, new()
                    {
                        Position = ray.GetPoint(hit.Distance),
                        Rotation = quaternion.LookRotation(math.normalizesafe(-projectile.ValueRW.Velocity), new float3(0f, 1f, 0f)),
                        Index = projectile.ValueRO.ImpactEffect,
                    });
                }
                commandBuffer.DestroyEntity(entity);
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
