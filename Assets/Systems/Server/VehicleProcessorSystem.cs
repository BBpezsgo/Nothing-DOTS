using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct VehicleProcessorSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, vehicle) in
            SystemAPI.Query<RefRO<Processor>, RefRW<Vehicle>>())
        {
            ref readonly MappedMemory mapped = ref processor.ValueRO.Memory.MappedMemory;

            vehicle.ValueRW.Input = new float2(
                mapped.Vehicle.InputSteer / 128f,
                mapped.Vehicle.InputForward / 128f
            );
        }
    }
}
