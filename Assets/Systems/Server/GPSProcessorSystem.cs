using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GPSProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, transform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<LocalToWorld>>()
            .WithAll<CoreComputer>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);
            mapped->GPS.Position = new(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
            mapped->GPS.Forward = new(transform.ValueRO.Forward.x, transform.ValueRO.Forward.z);
        }
    }
}
