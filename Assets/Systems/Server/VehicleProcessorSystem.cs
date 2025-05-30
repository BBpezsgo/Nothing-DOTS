using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct VehicleProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, vehicle) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Vehicle>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            vehicle.ValueRW.Input = new float2(
                mapped->Vehicle.InputSteer / 128f,
                mapped->Vehicle.InputForward / 128f
            );
        }
    }
}
