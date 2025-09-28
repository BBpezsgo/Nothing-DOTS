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
    [BurstCompile]
    public static quaternion AlignPreserveYawExact(quaternion rotation, float3 terrainNormal)
    {
        return AlignPreserveYawExact(rotation.ToEuler().y, terrainNormal);
    }

    [BurstCompile]
    public static quaternion AlignPreserveYawExact(float yawRadians, float3 terrainNormal)
    {
        float3 N = math.normalizesafe(terrainNormal, new float3(0f, 1f, 0f));

        float3 fXZ = new(math.sin(yawRadians), 0f, math.cos(yawRadians));

        float3 forward;
        if (math.abs(N.y) > 1e-4f)
        {
            float y = -math.dot(fXZ, N) / N.y;
            forward = math.normalize(new float3(fXZ.x, y, fXZ.z));
        }
        else
        {
            forward = math.normalize(fXZ - math.dot(fXZ, N) * N);
        }

        float3 right = math.normalize(math.cross(N, forward));

        return new quaternion(new float3x3(right, N, forward));
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
            transform.ValueRW.Rotation = AlignPreserveYawExact(transform.ValueRO.Rotation, normal);

            terrainUnit.ValueRW.LastPosition = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
        }

        foreach (var (transform, terrainBuilding) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<TerrainBuilding>>())
        {
            if (terrainBuilding.ValueRO.IsInitialized)
            { continue; }
            if (!terrainSystem.TrySample(new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z), out float h, out float3 normal, true))
            { continue; }

            transform.ValueRW.Position.y = h;

            float3 euler = transform.ValueRO.Rotation.ToEuler();
            float yaw = euler.y;

            transform.ValueRW.Rotation = AlignPreserveYawExact(yaw, normal);

            terrainBuilding.ValueRW.IsInitialized = true;
        }
    }
}
