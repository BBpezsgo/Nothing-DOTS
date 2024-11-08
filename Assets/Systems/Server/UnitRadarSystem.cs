using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct UnitRadarSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        var map = QuadrantSystem.GetMap(ref state);

        foreach (var (processor, localTransform, transform) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalTransform>, RefRO<LocalToWorld>>())
        {
            float3 direction = processor.ValueRO.RadarRequest;

            if (direction.Equals(default)) continue;

            direction = localTransform.ValueRO.TransformDirection(direction);

            processor.ValueRW.RadarRequest = default;

            float3 rayStart = transform.ValueRO.Position + (direction * 1.1f);
            float3 rayEnd = transform.ValueRO.Position + (direction * (Unit.RadarRadius - 1f));

            Ray ray = new(rayStart, rayEnd, Layers.All);

            if (!QuadrantRayCast.RayCast(map, ray, out Hit hit))
            {
                processor.ValueRW.RadarResponse = float.NaN;
                return;
            }

            float distance = math.distance(ray.GetPoint(hit.Distance), rayStart) + 1.1f;

            if (distance > Unit.RadarRadius) distance = float.NaN;

            processor.ValueRW.RadarResponse = distance;
        }
    }
}
