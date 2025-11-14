using Unity.Entities;
using Unity.Burst;
using LanguageCore.Runtime;
using Unity.Collections;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct FacilityProcessorSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, facility, techIn, techOut) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Facility>, DynamicBuffer<BufferedTechnologyHashIn>, DynamicBuffer<BufferedTechnologyHashOut>>())
        {
            ref MappedMemory mapped = ref processor.ValueRW.Memory.MappedMemory;

            switch (mapped.Facility.Signal)
            {
                case MappedMemory_Facility.SignalEnqueueHash:
                {
                    mapped.Facility.Signal = 0;
                    if (mapped.Facility.HashLocation != 0)
                    {
                        FixedBytes30 hash = Processor.GetMemoryPtr(ref processor.ValueRW)
                            .Get<FixedBytes30>(mapped.Facility.HashLocation);
                        techIn.Add(new() { Hash = hash });
                    }
                    break;
                }
                case MappedMemory_Facility.SignalDequeueHash:
                {
                    if (mapped.Facility.HashLocation != 0)
                    {
                        if (techOut.IsEmpty)
                        {
                            mapped.Facility.Signal = MappedMemory_Facility.SignalDequeueFailure;
                        }
                        else
                        {
                            mapped.Facility.Signal = MappedMemory_Facility.SignalDequeueSuccess;
                            BufferedTechnologyHashOut tech = techOut[0];
                            techOut.RemoveAt(0);
                            Processor.GetMemoryPtr(ref processor.ValueRW).Set(mapped.Facility.HashLocation, tech.Hash);
                        }
                    }
                    break;
                }
            }
        }
    }
}
