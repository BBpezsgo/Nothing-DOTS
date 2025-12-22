using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Pool;
using System.Diagnostics.CodeAnalysis;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class WireRendererSystem : SystemBase
{
    /*
    Entity _segments;

    protected override void OnCreate()
    {
        RequireForUpdate<WiresSettings>();
    }

    protected override void OnStopRunning()
    {
        Core.Destroy(EntityManager, _segments);
    }

    protected override void OnUpdate()
    {
        if (_segments == Entity.Null)
        {
            WiresSettings settings = SystemAPI.ManagedAPI.GetSingleton<WiresSettings>();
            Core.Create(EntityManager, out _segments, settings.Material);
        }

        Segment segment = Core.GetSegment(EntityManager, _segments);

        segment.Buffer.Clear();

        foreach (var (connector, connectorPos1, wires, ghost) in SystemAPI.Query<RefRO<Connector>, RefRO<LocalTransform>, DynamicBuffer<BufferedWire>, RefRO<GhostInstance>>().WithAll<Connector>())
        {
            foreach (BufferedWire connector2 in wires)
            {
                if (!connector2.GhostA.Equals(ghost.ValueRO)) continue;

                float3 connectorPos2 = default;
                foreach (var (_connector2, _connectorPos2, ghost2) in SystemAPI.Query<RefRO<Connector>, RefRO<LocalTransform>, RefRO<GhostInstance>>())
                {
                    if (!connector2.GhostB.Equals(ghost2.ValueRO)) continue;
                    connectorPos2 = _connectorPos2.ValueRO.TransformPoint(_connector2.ValueRO.ConnectorPosition);
                    break;
                }

                if (connectorPos2.Equals(default)) continue;

                float3 startPosition = connectorPos1.ValueRO.TransformPoint(connector.ValueRO.ConnectorPosition);
                float3 endPosition = connectorPos2;

                float l = math.distance(startPosition, endPosition);

                float3 prevPosition = startPosition;
                for (float i = 1f; i < l; i++)
                {
                    float3 p = math.lerp(startPosition, endPosition, i / l);
                    segment.Buffer.Add(new float3x2(prevPosition, p));
                    prevPosition = p;
                }
                segment.Buffer.Add(new float3x2(prevPosition, endPosition));
            }
        }

        Core.SetSegmentChanged(EntityManager, _segments);
    }
    */

    readonly struct WireId : IEquatable<WireId>
    {
        public readonly SpawnedGhost A;
        public readonly SpawnedGhost B;

        public WireId(BufferedWire v)
        {
            A = v.GhostA;
            B = v.GhostB;
        }


        public static implicit operator WireId(BufferedWire v) => new(v);

        public bool Equals(WireId other) => A.Equals(other.A) && B.Equals(other.B);
        public override bool Equals(object? obj) => obj is WireId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
    }

    [NotNull] WiresSettings? Settings = null;
    readonly Dictionary<WireId, LineRenderer> Lines = new();
    [NotNull] ObjectPool<LineRenderer>? LinesPool = null;

    static void OnReleaseLine(LineRenderer renderer)
    {
        renderer.gameObject.SetActive(false);
    }
    static void OnGetLine(LineRenderer renderer)
    {
        renderer.gameObject.SetActive(true);
    }
    LineRenderer CreateLine()
    {
        LineRenderer line = new GameObject("Wire").AddComponent<LineRenderer>();
        line.material = Settings.Material;
        line.widthCurve = AnimationCurve.Constant(0f, 1f, 0.05f);
        return line;
    }

    protected override void OnCreate()
    {
        RequireForUpdate<WiresSettings>();
        LinesPool = new(CreateLine, OnGetLine, OnReleaseLine);
    }

    protected override void OnUpdate()
    {
        Settings = SystemAPI.ManagedAPI.GetSingleton<WiresSettings>();

        foreach (KeyValuePair<WireId, LineRenderer> line in Lines)
        {
            bool a = false;
            bool b = false;

            foreach (RefRO<GhostInstance> ghost in SystemAPI.Query<RefRO<GhostInstance>>())
            {
                if (line.Key.A.Equals(ghost.ValueRO))
                {
                    a = true;
                    if (b) break;
                }
                else if (line.Key.B.Equals(ghost.ValueRO))
                {
                    b = true;
                    if (a) break;
                }
            }

            if (!a || !b)
            {
                LinesPool.Release(line.Value);
                Lines.Remove(line.Key);
                break;
            }
        }

        foreach (var (connector, connectorPos1, wires, ghost, entity) in SystemAPI.Query<RefRO<Connector>, RefRO<LocalTransform>, DynamicBuffer<BufferedWire>, RefRO<GhostInstance>>().WithEntityAccess())
        {
            foreach (BufferedWire wire in wires)
            {
                if (!wire.GhostA.Equals(ghost.ValueRO)) continue;

                float3 connectorPos2 = default;
                foreach (var (_connector2, _connectorPos2, ghost2) in SystemAPI.Query<RefRO<Connector>, RefRO<LocalTransform>, RefRO<GhostInstance>>())
                {
                    if (!wire.GhostB.Equals(ghost2.ValueRO)) continue;
                    connectorPos2 = _connectorPos2.ValueRO.TransformPoint(_connector2.ValueRO.ConnectorPosition);
                    break;
                }

                if (connectorPos2.Equals(default)) continue;

                WireId wireId = wire;

                if (!Lines.TryGetValue(wireId, out LineRenderer? line) || line == null)
                {
                    line = Lines[wireId] = LinesPool.Get();
                }

                float3 startPosition = connectorPos1.ValueRO.TransformPoint(connector.ValueRO.ConnectorPosition);
                float3 endPosition = connectorPos2;

                float l = math.distance(startPosition, endPosition);
                line.positionCount = (int)math.ceil(l) + 1;
                for (int i = 0; i < l; i++)
                {
                    float3 p = math.lerp(startPosition, endPosition, i / l);
                    line.SetPosition(i, p);
                }
                line.SetPosition(line.positionCount - 1, endPosition);
            }
        }
    }
}
