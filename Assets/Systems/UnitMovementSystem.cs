using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct UnitMovementSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<LocalTransform> transform, Entity entity) in
                    SystemAPI.Query<RefRW<LocalTransform>>()
                    .WithAll<Unit>()
                    .WithEntityAccess())
        {
            float3 pos = transform.ValueRO.Position;

            pos.y = (float)entity.Index;

            float angle = (0.5f + noise.cnoise(pos / 10f)) * 4.0f * math.PI;
            float3 dir = float3.zero;
            math.sincos(angle, out dir.x, out dir.z);

            transform.ValueRW.Position += dir * dt * 5.0f;
            transform.ValueRW.Rotation = quaternion.RotateY(angle);
        }
    }
}
