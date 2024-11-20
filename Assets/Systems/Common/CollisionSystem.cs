using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public unsafe partial struct CollisionSystem : ISystem
{
    /// <summary>
    /// <seealso href="https://www.khoury.northeastern.edu/home/fell/CS4300/Lectures/Ray-TracingFormulas.pdf">Source</seealso> 
    /// </summary>
    [BurstCompile]
    static bool RaycastSphere(
        in float sphereRadius, in float3 sphereOffset,
        in Ray ray,
        out float t
    )
    {
        float3 d = ray.End - ray.Start;

        float a = math.lengthsq(d);
        float b =
            2f * d.x * (ray.Start.x - sphereOffset.x) +
            2f * d.y * (ray.Start.y - sphereOffset.y) +
            2f * d.z * (ray.Start.z - sphereOffset.z);
        float c =
            math.lengthsq(sphereOffset) + math.lengthsq(ray.Start) +
            -2f * math.dot(sphereOffset, ray.Start) - sphereRadius * sphereRadius;

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            t = default;
            return false;
        }

        t = (-b - math.sqrt(b * b - 4f * a * c)) / (2f * a);
        return true;
    }

    /// <summary>
    /// <seealso href="https://gamedev.stackexchange.com/a/18459">Source</seealso>
    /// </summary>
    [BurstCompile]
    static bool RaycastAABB(
        in AABB aabb,
        in Ray ray,
        out float t)
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
            t = tmax;
            return false;
        }

        // if tmin > tmax, ray doesn't intersect AABB
        if (tmin > tmax)
        {
            t = tmax;
            return false;
        }

        t = tmin;
        return true;
    }

    [BurstCompile]
    public static bool Raycast(
        in Collider collider, in float3 offset,
        in Ray ray,
        out float t
    )
    {
        switch (collider.Type)
        {
            case ColliderType.Sphere:
                return RaycastSphere(
                    collider.Sphere.Radius, offset,
                    ray,
                    out t);
            case ColliderType.AABB:
                AABB aabb = collider.AABB.AABB;
                aabb.Center += offset;
                return RaycastAABB(
                    aabb,
                    ray,
                    out t);
            default: throw new UnreachableException();
        }
    }

    [BurstCompile]
    static bool CircleRectIntersect(
        in float3 sphereOrigin, float sphereRadius,
        in float3 aabbOrigin, in AABB aabb,
        out float3 normal, out float depth)
    {
        float nearestX = math.max(aabbOrigin.x + aabb.Min.x, math.min(sphereOrigin.x, aabbOrigin.x + aabb.Max.x));
        float nearestY = math.max(aabbOrigin.y + aabb.Min.y, math.min(sphereOrigin.y, aabbOrigin.y + aabb.Max.y));
        float nearestZ = math.max(aabbOrigin.z + aabb.Min.z, math.min(sphereOrigin.y, aabbOrigin.z + aabb.Max.z));
        float3 dist = new(
            sphereOrigin.x - nearestX,
            sphereOrigin.y - nearestY,
            sphereOrigin.z - nearestZ);

        if (dist.Equals(default))
        {
            depth = default;
            normal = default;
            return false;
        }

        float distLength = math.length(dist);

        depth = sphereRadius - distLength;
        normal = dist / distLength * depth;
        return depth > 0f;
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
        else
        {
            Debug.LogWarning(string.Format("Unsupported collision combination: {0} - {1}", a.Type, b.Type));
            normal = default;
            depth = default;
            return false;
        }
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

                    if (!Intersect(
                        a->Collider, a->Position,
                        b->Collider, b->Position,
                        out float3 normal, out float depth
                        )) continue;

                    normal.y = 0f;
                    depth = math.clamp(depth, 0f, 0.1f);

                    float3 displaceA = normal * (depth * 0.5f);
                    float3 displaceB = normal * (depth * -0.5f);

                    a->ResolvedOffset += displaceA;
                    a->Position += displaceA;

                    b->ResolvedOffset += displaceB;
                    b->Position += displaceB;
                }
                RefRW<LocalTransform> transformA = SystemAPI.GetComponentRW<LocalTransform>(a->Entity);
                transformA.ValueRW.Position += a->ResolvedOffset;
                a->ResolvedOffset = default;
            }
        }
        enumerator.Dispose();
    }
}
