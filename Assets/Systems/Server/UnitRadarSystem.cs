#if UNITY_EDITOR && EDITOR_DEBUG
#define _DEBUG_LINES
#endif

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct UnitRadarSystem : ISystem
{
    const float DebugDuration = 3f;

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
            float3 rayStart3 = transform.ValueRO.Position + (new float3(direction.x, 0f, direction.y) * offset);
            float2 rayEnd = position + (direction * (Radar.RadarRadius - offset));
            float3 rayEnd3 = transform.ValueRO.Position + (new float3(direction.x, 0f, direction.y) * (Radar.RadarRadius - offset));

            RadarRay ray = new(localTransform.ValueRO.Position, direction, Radar.RadarRadius - offset, Layers.BuildingOrUnit);

#if DEBUG_LINES
            Debug.DrawLine(rayStart3, rayEnd3, Color.white, DebugDuration);
#endif

            if (!RadarCast(map, ray, out RadarHit hit))
            {
                processor.ValueRW.RadarResponse = float.NaN;
                return;
            }

            float distance = math.distance(hit.Point, rayStart3) + offset;

#if DEBUG_LINES
            DebugEx.DrawPoint(hit.Point, 1f, Color.white, DebugDuration, false);
#endif

            if (distance > Radar.RadarRadius) processor.ValueRW.RadarResponse = float.NaN;
            else processor.ValueRW.RadarResponse = localTransform.ValueRO.InverseTransformPoint(hit.Point);
        }
    }

    [BurstCompile]
    public readonly struct RadarRay
    {
        public readonly float3 Start;
        public readonly float2 End;
        public readonly float MaxDistance;
        public readonly float2 Direction;
        public readonly uint Layer;
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool ExcludeContainingBodies;

        public RadarRay(float3 start, float2 direction, float maxDistance, uint layer, bool excludeContainingBodies = true)
        {
            Start = start;
            End = new float2(start.x, start.z) + direction * maxDistance;
            MaxDistance = maxDistance;
            Direction = direction;
            Layer = layer;
            ExcludeContainingBodies = excludeContainingBodies;
        }
    }

    [BurstCompile]
    public readonly struct RadarHit
    {
        public readonly QuadrantEntity Entity;
        public readonly float3 Point;

        public RadarHit(QuadrantEntity entity, float3 point)
        {
            Entity = entity;
            Point = point;
        }
    }

    [BurstCompile]
    static bool RadarCast(
        in NativeList<QuadrantEntity> entities,
        in RadarRay ray,
        out RadarHit hit)
    {
        for (int i = 0; i < entities.Length; i++)
        {
            if ((entities[i].Layer & ray.Layer) == 0u) continue;

            if (ray.ExcludeContainingBodies && CollisionSystem.Contains(
                    entities[i].Collider,
                    entities[i].Position,
                    ray.Start))
            { continue; }

            float3 target = entities[i].Position + entities[i].Collider.Type switch
            {
                ColliderType.Sphere => entities[i].Collider.Sphere.Offset,
                ColliderType.AABB => entities[i].Collider.AABB.AABB.Center,
                _ => default,
            };
            float3 direction = math.normalize(target - ray.Start);

            float yaw = math.atan2(ray.Direction.x, ray.Direction.y);
            float pitch = math.asin(direction.y);

            direction.x = math.cos(pitch) * math.sin(yaw);
            direction.y = math.sin(pitch);
            direction.z = math.cos(pitch) * math.cos(yaw);

            Ray ray3 = new(
                ray.Start,
                direction * ray.MaxDistance,
                ray.Layer,
                ray.ExcludeContainingBodies
            );

#if DEBUG_LINES
            Debug.DrawLine(ray3.Start, ray3.End, Color.white, DebugDuration);
#endif

            if (!CollisionSystem.Raycast(
                entities[i].Collider,
                entities[i].Position,
                ray3,
                out float distance))
            { continue; }

            float3 p = ray3.GetPoint(distance);

            if (distance > ray.MaxDistance)
            {
#if DEBUG_LINES
                DebugEx.DrawPoint(p, 2f, Color.orange, DebugDuration, false);
#endif
                continue;
            }

            hit = new(entities[i], p);
            return true;
        }

        hit = default;
        return false;
    }

    /// <remarks>
    /// Source: javidx9
    /// </remarks>
    [BurstCompile]
    static bool RadarCast(
        in NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map,
        in RadarRay ray,
        out RadarHit hit)
    {
        if (ray.MaxDistance <= 0f) { hit = default; return false; }

        Cell.ToGridF(ray.Start, out float2 _start);
        Cell.ToGridF(ray.End, out float2 _end);
        float2 _dir = math.normalize(_end - _start);

        float2 rayUnitStepSize = new(
            math.sqrt(1f + (_dir.y / _dir.x) * (_dir.y / _dir.x)),
            math.sqrt(1f + (_dir.x / _dir.y) * (_dir.x / _dir.y))
        );

        Cell.ToGrid(ray.Start, out Cell mapCheck);
        float2 rayLength1D;
        Cell step;

        if (_dir.x < 0f)
        {
            step.x = -1;
            rayLength1D.x = (_start.x - mapCheck.x) * rayUnitStepSize.x;
        }
        else
        {
            step.x = 1;
            rayLength1D.x = (mapCheck.x + 1 - _start.x) * rayUnitStepSize.x;
        }

        if (_dir.y < 0f)
        {
            step.y = -1;
            rayLength1D.y = (_start.y - mapCheck.y) * rayUnitStepSize.y;
        }
        else
        {
            step.y = 1;
            rayLength1D.y = (mapCheck.y + 1 - _start.y) * rayUnitStepSize.y;
        }

        float maxDistance = math.distance(_start, _end);
        float distance = 0f;

        while (distance < maxDistance)
        {
            if (map.TryGetValue(mapCheck.key, out NativeList<QuadrantEntity> cell) &&
                RadarCast(cell, ray, out hit))
            { return true; }

            if (rayLength1D.x < rayLength1D.y)
            {
                mapCheck.x += step.x;
                distance = rayLength1D.x;
                rayLength1D.x += rayUnitStepSize.x;
            }
            else
            {
                mapCheck.y += step.y;
                distance = rayLength1D.y;
                rayLength1D.y += rayUnitStepSize.y;
            }
        }

        hit = default;
        return false;
    }
}
