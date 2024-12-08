using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public unsafe partial struct CollisionSystem : ISystem
{
    [BurstCompile]
    public static void Debug(
        in Collider collider, in float3 offset,
        in Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        switch (collider.Type)
        {
            case ColliderType.Sphere:
                DebugEx.DrawSphere(offset, collider.Sphere.Radius, color, duration, depthTest);
                break;
            case ColliderType.AABB:
                AABB aabb = collider.AABB.AABB;
                aabb.Center += offset;
                DebugEx.DrawBox(aabb, color, duration, depthTest);
                break;
            default: throw new UnreachableException();
        }
#endif
    }

    [BurstCompile]
    public static bool Contains(
        in Collider collider, in float3 offset,
        in float3 point)
    {
        switch (collider.Type)
        {
            case ColliderType.Sphere:
                return math.distancesq(point, offset) <= collider.Sphere.Radius * collider.Sphere.Radius;
            case ColliderType.AABB:
                AABB aabb = collider.AABB.AABB;
                aabb.Center += offset;
                return aabb.Contains(point);
            default: throw new UnreachableException();
        }
    }

    /// <summary>
    /// <seealso href="https://github.com/xhacker/raycast/blob/master/raycast/sphere.cpp">Source</seealso> 
    /// </summary>
    [BurstCompile]
    static bool RaycastSphere(
        in float sphereRadius, in float3 sphereOffset,
        in Ray ray,
        out float distance)
    {
        float3 rayStartLocal = ray.Start - sphereOffset;

        float a = math.dot(ray.Direction, ray.Direction);
        float b = 2f * math.dot(ray.Direction, rayStartLocal);
        float c = math.dot(rayStartLocal, rayStartLocal);
        c -= sphereRadius * sphereRadius;

        float dt = b * b - 4f * a * c;

        if (dt < 0)
        {
            distance = default;
            return false;
        }

        distance = (-b - math.sqrt(dt)) / (a * 2f);
        return distance >= 0;
    }

    /// <summary>
    /// <seealso href="https://gamedev.stackexchange.com/a/18459">Source</seealso>
    /// </summary>
    [BurstCompile]
    static bool RaycastAABB(
        in AABB aabb,
        in Ray ray,
        out float distance)
    {
        // r.dir is unit direction vector of ray
        float3 dirfrac = 1f / ray.Direction;

        // lb is the corner of AABB with minimal coordinates - left bottom, rt is maximal corner
        // r.org is origin of ray
        float t1 = (aabb.Min.x - ray.Start.x) * dirfrac.x;
        float t2 = (aabb.Max.x - ray.Start.x) * dirfrac.x;
        float t3 = (aabb.Min.y - ray.Start.y) * dirfrac.y;
        float t4 = (aabb.Max.y - ray.Start.y) * dirfrac.y;
        float t5 = (aabb.Min.z - ray.Start.z) * dirfrac.z;
        float t6 = (aabb.Max.z - ray.Start.z) * dirfrac.z;

        float tmin = math.max(math.max(math.min(t1, t2), math.min(t3, t4)), math.min(t5, t6));
        float tmax = math.min(math.min(math.max(t1, t2), math.max(t3, t4)), math.max(t5, t6));

        // if tmax < 0, ray (line) is intersecting AABB, but the whole AABB is behind us
        if (tmax < 0)
        {
            distance = tmax;
            return false;
        }

        // if tmin > tmax, ray doesn't intersect AABB
        if (tmin > tmax)
        {
            distance = tmax;
            return false;
        }

        distance = tmin;
        return true;
    }

    [BurstCompile]
    public static bool Raycast(
        in Collider collider, in float3 offset,
        in Ray ray,
        out float distance)
    {
        switch (collider.Type)
        {
            case ColliderType.Sphere:
                return RaycastSphere(
                    collider.Sphere.Radius, offset,
                    ray,
                    out distance);
            case ColliderType.AABB:
                AABB aabb = collider.AABB.AABB;
                aabb.Center += offset;
                return RaycastAABB(
                    aabb,
                    ray,
                    out distance);
            default: throw new UnreachableException();
        }
    }

    [BurstCompile]
    static void ClosestPoint(
        in AABB aabb, in float3 point, out float3 closestPoint
    )
    {
        closestPoint = math.clamp(point, aabb.Min, aabb.Max);
    }

    [BurstCompile]
    static bool CircleRectIntersect(
        in float3 sphereOrigin, float sphereRadius,
        in float3 aabbOrigin, in AABB aabb,
        out float3 normal, out float depth)
    {
        depth = default;
        float3 closestPoint = math.clamp(sphereOrigin, aabb.Min + aabbOrigin, aabb.Max + aabbOrigin);
        normal = sphereOrigin - closestPoint;
        float distanceSquared = math.lengthsq(normal);
        float radiusSquared = sphereRadius * sphereRadius;
        if (distanceSquared >= radiusSquared)
        { return false; }
        float distance = math.sqrt(distanceSquared);
        if (distance == 0f)
        { return false; }

        normal /= distance;
        depth = sphereRadius - distance;
        return true;
    }

    [BurstCompile]
    static bool CircleCircleIntersect(
        in float3 originA, float radiusA,
        in float3 originB, float radiusB,
        out float3 normal, out float depth
    )
    {
        depth = default;
        normal = originA - originB;

        float distance = math.length(normal);
        if (distance == 0f) return false;

        float radii = radiusA + radiusB;

        if (distance > radii) return false;

        normal /= distance;
        depth = radii - distance;
        return true;
    }

    [BurstCompile]
    static bool AABBAABBIntersect(
        in float3 originA, in AABB a,
        in float3 originB, in AABB b,
        out float3 normal, out float depth
    )
    {
        AABB _a = a;
        AABB _b = b;

        _a.Center += originA;
        _b.Center += originB;

        normal = default;
        depth = default;

        if (
            _a.Min.x <= _b.Max.x &&
            _a.Max.x >= _b.Min.x &&

            _a.Min.y <= _b.Max.y &&
            _a.Max.y >= _b.Min.y &&

            _a.Min.z <= _b.Max.z &&
            _a.Max.z >= _b.Min.z
        )
        {
            // float dx = Math.Min(a.Max.x - b.Min.x, b.Max.x - a.Min.x);
            // float dy = Math.Min(a.Max.y - b.Min.y, b.Max.y - a.Min.y);
            // float dz = Math.Min(a.Max.z - b.Min.z, b.Max.z - a.Min.z);
            return true;
        }

        return false;
    }

    [BurstCompile]
    public static bool Intersect(
        in Collider a, in float3 positionA,
        in Collider b, in float3 positionB,
        out float3 normal, out float depth
    )
    {
        if (a.Type == ColliderType.Sphere &&
            b.Type == ColliderType.Sphere)
        {
            return CircleCircleIntersect(
                positionA, a.Sphere.Radius,
                positionB, b.Sphere.Radius,
                out normal, out depth
            );
        }
        else if (a.Type == ColliderType.Sphere &&
                 b.Type == ColliderType.AABB)
        {
            return CircleRectIntersect(
                positionA, a.Sphere.Radius,
                positionB, b.AABB.AABB,
                out normal, out depth
            );
        }
        else if (a.Type == ColliderType.AABB &&
                 b.Type == ColliderType.Sphere)
        {
            return CircleRectIntersect(
                positionB, b.Sphere.Radius,
                positionA, a.AABB.AABB,
                out normal, out depth
            );
        }
        else if (a.Type == ColliderType.AABB &&
                 b.Type == ColliderType.AABB)
        {
            return AABBAABBIntersect(
                positionA, a.AABB.AABB,
                positionB, b.AABB.AABB,
                out normal, out depth
            );
        }
        else
        {
            UnityEngine.Debug.LogWarning(string.Format("Unsupported collision combination: {0} - {1}", a.Type, b.Type));
            normal = default;
            depth = default;
            return false;
        }
    }

    [BurstCompile]
    public static bool Intersect(
        in Unity.Collections.NativeParallelHashMap<uint, Unity.Collections.NativeList<QuadrantEntity>>.ReadOnly quadrantMap,
        in Collider collider, in float3 colliderPosition,
        out float3 normal, out float depth)
    {
        normal = default;
        depth = default;

        if (!quadrantMap.TryGetValue(QuadrantSystem.ToGrid(colliderPosition).key, out var quadrant)) return false;

        for (int i = 0; i < quadrant.Length; i++)
        {
            if (Intersect(quadrant[i].Collider, quadrant[i].Position, collider, colliderPosition, out normal, out depth))
            { return true; }
        }

        return false;
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        var map = QuadrantSystem.GetMap(ref state);

        var enumerator = map.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var pair = enumerator.Current;
            for (int i = 0; i < pair.Value.Length; i++)
            {
                QuadrantEntity* a = &pair.Value.GetUnsafePtr()[i];
                a->ResolvedOffset = default;
                for (int j = i + 1; j < pair.Value.Length; j++)
                {
                    QuadrantEntity* b = &pair.Value.GetUnsafePtr()[j];

                    if (a->Collider.IsStatic && b->Collider.IsStatic) continue;

                    if (!Intersect(
                        a->Collider, a->Position,
                        b->Collider, b->Position,
                        out float3 normal, out float depth
                        )) continue;

                    Debug(a->Collider, a->Position, Color.green, 0.1f, false);
                    Debug(b->Collider, b->Position, Color.green, 0.1f, false);

                    normal.y = 0f;
                    depth = math.clamp(depth, 0f, 0.1f);

                    if (a->Collider.IsStatic)
                    {
                        float3 displaceB = normal * depth;

                        b->ResolvedOffset += displaceB;
                        b->Position += displaceB;
                    }
                    else if (b->Collider.IsStatic)
                    {
                        float3 displaceA = normal * depth;

                        a->ResolvedOffset += displaceA;
                        a->Position += displaceA;
                    }
                    else
                    {
                        float3 displaceA = normal * (depth * 0.5f);
                        float3 displaceB = normal * (depth * -0.5f);

                        a->ResolvedOffset += displaceA;
                        a->Position += displaceA;

                        b->ResolvedOffset += displaceB;
                        b->Position += displaceB;
                    }
                }
                RefRW<LocalTransform> transformA = SystemAPI.GetComponentRW<LocalTransform>(a->Entity);
                transformA.ValueRW.Position += a->ResolvedOffset;
                a->ResolvedOffset = default;
            }
        }
        enumerator.Dispose();
    }
}
