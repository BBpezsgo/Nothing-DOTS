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
            float2 direction = processor.ValueRO.RadarRequest;
            if (direction.Equals(default)) continue;

            float3 direction3 = localTransform.ValueRO.TransformDirection(new float3(direction.x, 0f, direction.y));
            direction = math.normalize(new float2(direction3.x, direction3.z));
            float2 position = new(transform.ValueRO.Position.x, transform.ValueRO.Position.z);

            processor.ValueRW.RadarRequest = default;

            processor.ValueRW.RadarLED.Blink();

            const float offset = 0f;

            float2 rayStart = position + (direction * offset);
            float2 rayEnd = position + (direction * (Radar.RadarRadius - offset));

            Ray2 ray = new(rayStart, rayEnd, Layers.BuildingOrUnit);

#if DEBUG_LINES
            Debug.DrawLine(new UnityEngine.Vector3(rayStart.x, 0f, rayStart.y), new UnityEngine.Vector3(rayEnd.x, 0f, rayEnd.y), Color.white, 1f);
#endif

            if (!QuadrantRayCast.RayCast(map, ray, out Hit hit))
            {
                processor.ValueRW.RadarResponse = float.NaN;
                return;
            }

            var p = ray.GetPoint(hit.Distance);

            float distance = math.distance(p, rayStart) + offset;

#if DEBUG_LINES
            DebugEx.DrawPoint(new float3(p.x, 0f, p.y), 1f, Color.white, 1f, false);
#endif

            if (distance > Radar.RadarRadius) distance = float.NaN;

            processor.ValueRW.RadarResponse = distance;
        }
    }
}
