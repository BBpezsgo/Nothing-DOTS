using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public static class QuadrantRayCast
{
    [BurstCompile]
    static bool RayCast_(
        in NativeList<QuadrantEntity> entities,
        in Ray ray,
        out Hit hit)
    {
        AABB aabb = new()
        {
            Extents = new float3(1f, 1f, 1f),
        };
        for (int i = 0; i < entities.Length; i++)
        {
            if ((entities[i].Layer & ray.Layer) == 0u) continue;
            aabb.Center = entities[i].Position;
            if (ray.ExcludeContainingBodies && aabb.Contains(ray.Start)) continue;
            // DebugEx.DrawBox(aabb, QuadrantSystem.CellColor(entities[i].Key));
            if (CollisionSystem.Raycast(entities[i].Collider, entities[i].Position, ray, out float distance) &&
                (distance * distance) < math.distancesq(ray.End, ray.Start))
            {
                // DebugEx.DrawPoint(ray.GetPoint(distance), 1f, Color.red, 1f);
                hit = new(entities[i], distance);
                return true;
            }
        }

        hit = default;
        return false;
    }

    /// <remarks>
    /// Source: javidx9
    /// </remarks>
    [BurstCompile]
    public static bool RayCast(
        in NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map,
        in Ray ray,
        out Hit hit)
    {
        // DebugEx.DrawPoint(start, 1f, Color.red);
        // Debug.DrawLine(ray.Start, ray.End, Color.red, 1f);

        if (ray.Start.Equals(ray.End)) { hit = default; return false; }

        QuadrantSystem.ToGridF(ray.Start, out float2 _start);
        QuadrantSystem.ToGridF(ray.End, out float2 _end);
        float2 _dir = math.normalize(_end - _start);

        float2 rayUnitStepSize = new(
            math.sqrt(1f + (_dir.y / _dir.x) * (_dir.y / _dir.x)),
            math.sqrt(1f + (_dir.x / _dir.y) * (_dir.x / _dir.y))
        );

        QuadrantSystem.ToGrid(ray.Start, out Cell mapCheck);
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
            // QuadrantSystem.DrawQuadrant(mapCheck);
            if (map.TryGetValue(mapCheck.key, out NativeList<QuadrantEntity> cell) &&
                RayCast_(cell, ray, out hit))
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
