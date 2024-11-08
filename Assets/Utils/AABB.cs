using Unity.Mathematics;

public static class AABB
{
    /// <summary>
    /// <seealso href="https://gamedev.stackexchange.com/a/18459">Source</seealso>
    /// </summary>
    public static bool RayCast(Unity.Mathematics.AABB aabb, float3 org, float3 dir, out float t)
    {
        // r.dir is unit direction vector of ray
        float3 dirfrac;
        var lb = aabb.Min;
        var rt = aabb.Max;

        dirfrac.x = 1.0f / dir.x;
        dirfrac.y = 1.0f / dir.y;
        dirfrac.z = 1.0f / dir.z;
        // lb is the corner of AABB with minimal coordinates - left bottom, rt is maximal corner
        // r.org is origin of ray
        float t1 = (lb.x - org.x) * dirfrac.x;
        float t2 = (rt.x - org.x) * dirfrac.x;
        float t3 = (lb.y - org.y) * dirfrac.y;
        float t4 = (rt.y - org.y) * dirfrac.y;
        float t5 = (lb.z - org.z) * dirfrac.z;
        float t6 = (rt.z - org.z) * dirfrac.z;

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
}
