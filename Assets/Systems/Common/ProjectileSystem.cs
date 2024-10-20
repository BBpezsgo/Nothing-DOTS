using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#nullable enable

[BurstCompile]
partial struct ProjectileSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        ProjectileJob projectileJob = new()
        {
            EntityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged),
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        projectileJob.Schedule();
    }
}

[BurstCompile]
public partial struct ProjectileJob : IJobEntity
{
    public EntityCommandBuffer EntityCommandBuffer;
    public float DeltaTime;

    [BurstCompile]
    void Execute(Entity entity, ref Projectile projectile, ref LocalTransform transform)
    {
        float3 gravity = new(0.0f, -9.82f, 0.0f);
        float3 invertY = new(1.0f, -1.0f, 1.0f);

        transform.Position += projectile.Velocity * DeltaTime;

        if (transform.Position.y < 0.0f)
        {
            transform.Position *= invertY;
            projectile.Velocity *= invertY * 0.8f;
        }

        projectile.Velocity += gravity * DeltaTime;

        float speed = math.lengthsq(projectile.Velocity);
        if (speed < 0.1f)
        {
            EntityCommandBuffer.DestroyEntity(entity);
        }
    }
}
