using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// [UpdateInGroup(typeof(TransformSystemGroup))]
// [UpdateBefore(typeof(LocalToWorldSystem))]
[BurstCompile]
public partial struct VehicleSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, vehicle) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRW<Vehicle>>())
        {
            vehicle.ValueRW.Speed = math.lerp(vehicle.ValueRO.Speed, vehicle.ValueRO.Input.y * Vehicle.MaxSpeed, deltaTime);
            if (math.abs(vehicle.ValueRO.Speed) <= 0.001f && math.abs(vehicle.ValueRO.Input.x) <= 0.001f) continue;

            transform.ValueRW.Position += vehicle.ValueRO.Speed * deltaTime * transform.ValueRO.Forward();
            transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.RotateY(math.radians(vehicle.ValueRO.Input.x * Vehicle.SteerSpeed * deltaTime)));
        }
    }
}
