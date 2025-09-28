using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GPSProcessorSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, transform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<LocalToWorld>>())
        {
            ref MappedMemory mapped = ref processor.ValueRW.Memory.MappedMemory;

            mapped.GPS.Position = transform.ValueRO.Position;
            mapped.GPS.Forward = transform.ValueRO.Forward;
        }
    }
}
