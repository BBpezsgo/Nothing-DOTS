using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct VehicleProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, vehicle, transform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Vehicle>, RefRW<LocalToWorld>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            vehicle.ValueRW.Input = new float2(
                mapped->Vehicle.InputSteer / 128f,
                mapped->Vehicle.InputForward / 128f
            );

            mapped->GPS.Position = new(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
            mapped->GPS.Forward = new(transform.ValueRO.Forward.x, transform.ValueRO.Forward.z);
        }
    }
}
