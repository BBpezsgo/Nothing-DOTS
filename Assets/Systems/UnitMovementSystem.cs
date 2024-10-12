using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct UnitMovementSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<LocalTransform> transform, RefRW<Unit> unit) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRW<Unit>>())
        {
            unit.ValueRW.Speed = math.lerp(unit.ValueRO.Speed, unit.ValueRO.Input.y, deltaTime);

            transform.ValueRW.Position += unit.ValueRO.Speed * deltaTime * transform.ValueRO.Forward();
            transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.RotateY(math.radians(unit.ValueRO.Input.x * deltaTime)));
        }
    }
}
