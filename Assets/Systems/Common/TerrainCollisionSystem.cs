using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TerrainCollisionSystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        var terrainSystem = TerrainSystemServer.GetInstance(state.WorldUnmanaged);
        foreach (var (transform, terrainUnit) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<TerrainUnit>>())
        {
            if (math.abs(transform.ValueRO.Position.x - terrainUnit.ValueRO.LastPosition.x) <= 0.01f &&
                math.abs(transform.ValueRO.Position.z - terrainUnit.ValueRO.LastPosition.y) <= 0.01f)
            { continue; }
            if (!terrainSystem.TrySample(new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z), out float h, out float3 normal, true))
            { continue; }

            if (transform.ValueRO.Position.y < h) transform.ValueRW.Position.y = h;

            float yaw = transform.ValueRO.Rotation.Yaw();

            // 1. Desired forward from yaw (ignoring terrain)
            float3 forward = new(math.sin(yaw), 0, math.cos(yaw));

            // 2. Recompute orthogonal basis
            float3 right = math.normalize(math.cross(normal, forward));
            forward = math.normalize(math.cross(right, normal));
            float3 up = math.normalize(normal);

            // 3. Construct rotation matrix
            float3x3 rotMatrix = new(right, up, forward);

            transform.ValueRW.Rotation = new(rotMatrix);

            terrainUnit.ValueRW.LastPosition = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
        }
    }
}
