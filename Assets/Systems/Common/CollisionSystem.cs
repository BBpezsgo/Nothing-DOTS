using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateBefore(typeof(ProcessorSystemServer))]
[UpdateBefore(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public unsafe partial struct CollisionSystem : ISystem
{
    [BurstCompile]
    static bool CircleIntersect(
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
    void ISystem.OnUpdate(ref SystemState state)
    {
        var map = QuadrantSystem.GetMap(ref state);

        var enumerator = map.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var pair = enumerator.Current;
            for (int i = 0; i < pair.Value.Length; i++)
            {
                var a = &pair.Value.GetUnsafePtr()[i];
                a->ResolvedOffset = default;
                for (int j = i + 1; j < pair.Value.Length; j++)
                {
                    var b = &pair.Value.GetUnsafePtr()[j];

                    if (!CircleIntersect(
                        a->Position, 1f,
                        b->Position, 1f,
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