#if UNITY_EDITOR && EDITOR_DEBUG
#define DEBUG_LINES
#endif

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct WiredTransmissionSystem : ISystem
{
    ComponentLookup<Processor> processorComponentQ;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        processorComponentQ = state.GetComponentLookup<Processor>(false);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        processorComponentQ.Update(ref state);

        NativeQueue<Entity> openSet = new(Allocator.Temp);
        NativeHashSet<Entity> closedSet = new(16, Allocator.Temp);

        foreach (var (processor, transform, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalTransform>>()
            .WithEntityAccess())
        {
            if (processor.ValueRW.OutgoingTransmissions.Length == 0) continue;
            BufferedUnitTransmissionOutgoing transmission = processor.ValueRW.OutgoingTransmissions[0];
            if (transmission.Metadata.IsWireless) continue;

            processor.ValueRW.OutgoingTransmissions.RemoveAt(0);

            if (!SystemAPI.HasComponent<Connector>(entity))
            {
                Debug.LogWarning("Processor has no connector but tried to transmit data");
                continue;
            }

            openSet.Clear();
            closedSet.Clear();

            closedSet.Add(entity);
            openSet.Enqueue(entity);

            while (openSet.TryDequeue(out Entity next))
            {
                DynamicBuffer<BufferedWire> buffer = SystemAPI.GetBuffer<BufferedWire>(next);
                for (int i = 0; i < buffer.Length; i++)
                {
                    BufferedWire wire = buffer[i];
                    if (wire.EntityA == next)
                    {
                        if (closedSet.Add(wire.EntityB))
                        {
#if DEBUG_LINES
                            float3 a = SystemAPI.GetComponentRO<LocalTransform>(next).ValueRO.TransformPoint(SystemAPI.GetComponentRO<Connector>(next).ValueRO.ConnectorPosition);
                            float3 b = SystemAPI.GetComponentRO<LocalTransform>(wire.EntityB).ValueRO.TransformPoint(SystemAPI.GetComponentRO<Connector>(wire.EntityB).ValueRO.ConnectorPosition);
                            WireRendererSystem.DrawWire(a, b, Color.white, 0.1f, false);
#endif
                            openSet.Enqueue(wire.EntityB);
                        }
                    }
                    else
                    {
                        if (closedSet.Add(wire.EntityA))
                        {
#if DEBUG_LINES
                            float3 a = SystemAPI.GetComponentRO<LocalTransform>(next).ValueRO.TransformPoint(SystemAPI.GetComponentRO<Connector>(next).ValueRO.ConnectorPosition);
                            float3 b = SystemAPI.GetComponentRO<LocalTransform>(wire.EntityA).ValueRO.TransformPoint(SystemAPI.GetComponentRO<Connector>(wire.EntityA).ValueRO.ConnectorPosition);
                            WireRendererSystem.DrawWire(a, b, Color.white, 0.1f, false);
#endif
                            openSet.Enqueue(wire.EntityA);
                        }
                    }
                }
            }

            processor.ValueRW.NetworkSendLED.Blink();

            closedSet.Remove(entity);
            foreach (Entity connector in closedSet)
            {
                RefRW<Processor> other = processorComponentQ.GetRefRWOptional(connector);
                if (!other.IsValid || !other.ValueRO.Source.Code.IsCreated || other.ValueRO.Signal != LanguageCore.Runtime.Signal.None) continue;

                other.ValueRW.NetworkReceiveLED.Blink();

#if DEBUG_LINES
                float3 p = SystemAPI.GetComponentRO<LocalTransform>(connector).ValueRO.TransformPoint(SystemAPI.GetComponentRO<Connector>(connector).ValueRO.ConnectorPosition);
                DebugEx.DrawSphere(p, 0.5f, Color.white, 0.1f, false);
#endif

                ref FixedList128Bytes<BufferedUnitTransmission> transmissions = ref other.ValueRW.IncomingTransmissions;

                if (transmissions.Length >= transmissions.Capacity) transmissions.RemoveAt(0);
                transmissions.Add(new()
                {
                    Data = transmission.Data,
                    Metadata = new IncomingUnitTransmissionMetadata()
                    {
                        IsWireless = false,
                        Wired = new(),
                    },
                });
            }
        }
    }
}
