using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct TerrainCollisionSystem : ISystem
{
    public static quaternion AlignPreserveYawExact(float yawRadians, float3 terrainNormal)
    {
        // Normalize the normal; fall back to world up if bad.
        float3 N = math.normalizesafe(terrainNormal, new float3(0f, 1f, 0f));

        // Desired planar forward from yaw (unit, horizontal).
        float3 fXZ = new(math.sin(yawRadians), 0f, math.cos(yawRadians));

        // Build a forward vector whose XZ is exactly fXZ, and that is orthogonal to N.
        // Solve dot(v, N) = 0 with v = (fXZ.x, y, fXZ.z) => y = -dot(fXZ, N) / N.y.
        float3 forward;
        const float eps = 1e-4f;
        if (math.abs(N.y) > eps)
        {
            float y = -math.dot(fXZ, N) / N.y;
            forward = math.normalize(new float3(fXZ.x, y, fXZ.z));
        }
        else
        {
            // Near-vertical normal (rare in terrains): fall back to tangent projection.
            forward = math.normalize(fXZ - math.dot(fXZ, N) * N);
        }

        // Right = N × forward (right-handed). Safe because forward ⟂ N.
        float3 right = math.normalize(math.cross(N, forward));

        // Construct rotation (float3x3 columns are the basis axes: right, up, forward).
        return new quaternion(new float3x3(right, N, forward));
        // Alternatively: return quaternion.LookRotationSafe(forward, N);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        TerrainSystemServer terrainSystem = TerrainSystemServer.GetInstance(state.WorldUnmanaged);

        foreach (var (transform, terrainUnit) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<TerrainUnit>>())
        {
            if (math.abs(transform.ValueRO.Position.x - terrainUnit.ValueRO.LastPosition.x) <= 0.01f &&
                math.abs(transform.ValueRO.Position.z - terrainUnit.ValueRO.LastPosition.y) <= 0.01f)
            { continue; }
            if (!terrainSystem.TrySample(new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z), out float h, out float3 normal, true))
            { continue; }

            transform.ValueRW.Position.y = h;

            float3 euler = transform.ValueRO.Rotation.ToEuler();
            float yaw = euler.y;

            transform.ValueRW.Rotation = AlignPreserveYawExact(yaw, normal);

            terrainUnit.ValueRW.LastPosition = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
        }
    }
}
