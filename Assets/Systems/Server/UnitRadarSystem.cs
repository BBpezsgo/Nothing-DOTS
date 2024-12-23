#if UNITY_EDITOR && EDITOR_DEBUG
#define _DEBUG_LINES
#endif

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
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalTransform>, RefRO<LocalToWorld>>()
            .WithAll<Radar>())
        {
            float3 direction = processor.ValueRO.RadarRequest;
            if (direction.Equals(default)) continue;
            direction = localTransform.ValueRO.TransformDirection(direction);
            processor.ValueRW.RadarRequest = default;

            processor.ValueRW.RadarLED.Blink();

            const float offset = 0f;

            float3 rayStart = transform.ValueRO.Position + (direction * offset);
            float3 rayEnd = transform.ValueRO.Position + (direction * (Radar.RadarRadius - offset));

            Ray ray = new(rayStart, rayEnd, Layers.BuildingOrUnit);

#if DEBUG_LINES
            Debug.DrawLine(rayStart, rayEnd, Color.white, 1f);
#endif

            if (!QuadrantRayCast.RayCast(map, ray, out Hit hit))
            {
                processor.ValueRW.RadarResponse = float.NaN;
                return;
            }

            float distance = math.distance(ray.GetPoint(hit.Distance), rayStart) + offset;

#if DEBUG_LINES
            DebugEx.DrawPoint(ray.GetPoint(hit.Distance), 1f, Color.white, 1f, false);
#endif

            if (distance > Radar.RadarRadius) distance = float.NaN;

            processor.ValueRW.RadarResponse = distance;
        }
    }
}
