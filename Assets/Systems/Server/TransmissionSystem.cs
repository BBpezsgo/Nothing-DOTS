#if UNITY_EDITOR && EDITOR_DEBUG
#define _DEBUG_LINES
#endif

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct TransmissionSystem : ISystem
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

        foreach (var (processor, transform, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            if (processor.ValueRW.OutgoingTransmissions.Length == 0) continue;
            var transmission = processor.ValueRW.OutgoingTransmissions[0];
            processor.ValueRW.OutgoingTransmissions.RemoveAt(0);

            NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map = QuadrantSystem.GetMap(state.WorldUnmanaged);
            Cell grid = QuadrantSystem.ToGrid(transform.ValueRO.Position);

            processor.ValueRW.NetworkSendLED.Blink();

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
#endif

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (!map.TryGetValue((grid + new Cell(x, z)).key, out NativeList<QuadrantEntity> cell)) continue;
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
