using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[UpdateBefore(typeof(TerrainCollisionSystem))]
partial struct VehicleSystem : ISystem
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

            float steer = math.radians(vehicle.ValueRO.Input.x * Vehicle.SteerSpeed * deltaTime);

            float3 forward = math.rotate(transform.ValueRO.Rotation, new float3(0, 0, 1));
            float3 up = transform.ValueRO.Up();

            float3 newForward = math.rotate(quaternion.AxisAngle(up, steer), forward);

            transform.ValueRW.Rotation = quaternion.LookRotationSafe(newForward, up);

            //transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, quaternion.AxisAngle(transform.ValueRO.Up(), steer));
        }
    }
}
