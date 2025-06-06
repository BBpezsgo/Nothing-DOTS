#if UNITY_EDITOR && EDITOR_DEBUG
#define _DEBUG_LINES
#endif

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct TransmissionSystem : ISystem
{
    ComponentLookup<Processor> processorComponentQ;

    static readonly ProfilerMarker _CellVisibilityCheck = new(ProfilerCategory.Scripts, "TransmissionSystem.CellVisibilityCheck");
    static readonly ProfilerMarker _CellOperation = new(ProfilerCategory.Scripts, "TransmissionSystem.CellOperation");

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        processorComponentQ = state.GetComponentLookup<Processor>(false);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        processorComponentQ.Update(ref state);

        foreach (var (processor, transform, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            if (processor.ValueRW.OutgoingTransmissions.Length == 0) continue;
            var transmission = processor.ValueRW.OutgoingTransmissions[0];
            processor.ValueRW.OutgoingTransmissions.RemoveAt(0);

            NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map = QuadrantSystem.GetMap(state.WorldUnmanaged);
            Cell grid = Cell.ToGrid(transform.ValueRO.Position);

            processor.ValueRW.NetworkSendLED.Blink();

            float2 position = new float2(transform.ValueRO.Position.x, transform.ValueRO.Position.z);

#if DEBUG_LINES
            if (!transmission.Direction.Equals(float3.zero))
            {
                DebugEx.DrawFOV(
                    transmission.Source,
                    transmission.Direction,
                    transmission.Angle,
                    Unit.TransmissionRadius,
                    Color.white,
                    1f,
                    false);
            }
            else
            {
                DebugEx.DrawSphere(
                    transmission.Source,
                    Unit.TransmissionRadius,
                    Color.white,
                    1f,
                    false);
            }
#endif

            int diff = (int)(Unit.TransmissionRadius / Cell.Size * 0.5f + 2f);

            for (int x = -diff; x <= diff; x++)
            {
                for (int z = -diff; z <= diff; z++)
                {
                    Cell p = grid + new Cell(x, z);
                    /*
                    using (var marker = _CellVisibilityCheck.Auto())
                    {
                        if (PointSquareDistance(position, Cell.TL(p), Cell.BR(p)) + 3 f > Unit.TransmissionRadius)
                        {
                            continue;
                        }
                        else if (!transmission.Direction.Equals(float3.zero))
                        {
                            if ((x != 0 || z != 0) && !IsSquareVisible(
                                position,
                                new float2(transmission.Direction.x, transmission.Direction.z),
                                transmission.Angle,
                                Cell.TL(p),
                                Cell.BR(p),
                                Unit.TransmissionRadius))
                            { continue; }
                        }
                    }
                    */

#if DEBUG_LINES
                    Cell.Draw(p, 1f);
#endif

                    if (!map.TryGetValue(p.key, out NativeList<QuadrantEntity> cell)) continue;

                    using (var marker = _CellOperation.Auto())
                    {
                        for (int i = 0; i < cell.Length; i++)
                        {
                            if (cell[i].Entity == entity) continue;

                            float3 entityLocalPosition = cell[i].Position;
                            entityLocalPosition.x = cell[i].Position.x - transform.ValueRO.Position.x;
                            entityLocalPosition.y = 0f;
                            entityLocalPosition.z = cell[i].Position.z - transform.ValueRO.Position.z;

                            float entityDistanceSq = math.lengthsq(entityLocalPosition);
                            if (entityDistanceSq > (Unit.TransmissionRadius * Unit.TransmissionRadius)) continue;

                            if (!transmission.Direction.Equals(float3.zero))
                            {
                                float dot = math.abs(math.dot(transmission.Direction, math.normalize(entityLocalPosition)));
                                if (dot < transmission.CosAngle)
                                {
                                    // Debug.Log(string.Format("{0} > {1}", dot, transmission.CosAngle));
                                    continue;
                                }
                            }

                            RefRW<Processor> other = processorComponentQ.GetRefRWOptional(cell[i].Entity);
                            if (!other.IsValid) continue;
                            if (other.ValueRO.SourceFile == default) continue;

                            other.ValueRW.NetworkReceiveLED.Blink();

                            ref var transmissions = ref other.ValueRW.IncomingTransmissions;

                            if (transmissions.Length >= transmissions.Capacity) transmissions.RemoveAt(0);
                            transmissions.Add(new()
                            {
                                Source = transform.ValueRO.Position,
                                Data = transmission.Data,
                            });
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    static float PointSquareDistance(in float2 point, in float2 a, in float2 b)
    {
        float dx = MathF.Max(MathF.Max(a.x - point.x, point.x - b.x), 0f);
        float dy = MathF.Max(MathF.Max(a.y - point.y, point.y - b.y), 0f);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    [BurstCompile]
    static bool IsSquareVisible(in float2 viewPos, in float2 viewDir, float fovAngle, in float2 a, in float2 b, float viewRange = float.MaxValue)
    {
        if (PointInRectangle(viewPos, a, b)) return true;

        ReadOnlySpan<float2> square = stackalloc float2[]
        {
            a,
            new float2(a.x, b.y),
            new float2(b.x, a.y),
            b,
        };

        float halfFov = fovAngle / 2f;
        float2 leftDir;
        float2 rightDir;
        {
            float cos = MathF.Cos(halfFov);
            float sin = MathF.Sin(-halfFov);
            leftDir = new float2(viewDir.x * cos - viewDir.y * sin, viewDir.x * sin + viewDir.y * cos);
            sin = MathF.Sin(halfFov);
            rightDir = new float2(viewDir.x * cos - viewDir.y * sin, viewDir.x * sin + viewDir.y * cos);
        }

        float2 leftEnd = viewPos + leftDir * viewRange;
        float2 rightEnd = viewPos + rightDir * viewRange;

        ReadOnlySpan<float2> fovTriangle = stackalloc float2[] {
            viewPos,
            leftEnd,
            rightEnd ,
        };

        for (int i = 0; i < 4; i++)
        {
            if (SegmentIntersectsTriangle(square[i], square[(i + 1) % 4], fovTriangle)) return true;
            if (PointInTriangle(square[i], viewPos, leftEnd, rightEnd)) return true;
        }

        return false;
    }

    [BurstCompile]
    static bool SegmentIntersectsTriangle(in float2 p1, in float2 p2, ReadOnlySpan<float2> tri)
    {
        for (int i = 0; i < 3; i++)
        {
            float2 q1 = tri[i];
            float2 q2 = tri[(i + 1) % 3];
            if (SegmentsIntersect(p1, p2, q1, q2))
            {
                return true;
            }
        }
        return false;
    }

    [BurstCompile]
    static bool CCW(in float2 a, in float2 b, in float2 c) => (c.y - a.y) * (b.x - a.x) > (b.y - a.y) * (c.x - a.x);

    [BurstCompile]
    static bool SegmentsIntersect(in float2 p1, in float2 p2, in float2 q1, in float2 q2) => (CCW(p1, q1, q2) != CCW(p2, q1, q2)) && (CCW(p1, p2, q1) != CCW(p1, p2, q2));

    [BurstCompile]
    static bool PointInTriangle(in float2 point, in float2 a, in float2 b, in float2 c)
    {
        float2 v0 = c - a;
        float2 v1 = b - a;

        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);

        float denom = d00 * d11 - d01 * d01;
        if (MathF.Abs(denom) <= 0.0001f) return false;

        float2 v2 = point - a;
        float d02 = math.dot(v0, v2);
        float d12 = math.dot(v1, v2);

        float u = (d11 * d02 - d01 * d12) / denom;
        float v = (d00 * d12 - d01 * d02) / denom;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    [BurstCompile]
    static bool PointInRectangle(in float2 point, in float2 a, in float2 b) =>
        point.x >= a.x && point.x <= b.x &&
        point.y >= a.y && point.y <= b.y;
}
