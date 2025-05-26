using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Burst;
using LanguageCore.Runtime;
using Unity.Collections;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FacilityProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, facility, techIn, techOut) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Facility>, DynamicBuffer<BufferedTechnologyHashIn>, DynamicBuffer<BufferedTechnologyHashOut>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            switch (mapped->Facility.Signal)
            {
                case MappedMemory_Facility.SignalEnqueueHash:
                    {
                        mapped->Facility.Signal = 0;
                        if (mapped->Facility.HashLocation != 0)
                        {
                            FixedBytes30 hash = Processor.GetMemoryPtr(ref processor.ValueRW)
                                .Get<FixedBytes30>(mapped->Facility.HashLocation);
                            techIn.Add(new() { Hash = hash });
                        }
                        break;
                    }
                case MappedMemory_Facility.SignalDequeueHash:
                    {
                        if (mapped->Facility.HashLocation != 0)
                        {
                            if (techOut.IsEmpty)
                            {
                                mapped->Facility.Signal = MappedMemory_Facility.SignalDequeueFailure;
                            }
                            else
                            {
                                mapped->Facility.Signal = MappedMemory_Facility.SignalDequeueSuccess;
                                BufferedTechnologyHashOut tech = techOut[0];
                                techOut.RemoveAt(0);
                                Processor.GetMemoryPtr(ref processor.ValueRW).Set(mapped->Facility.HashLocation, tech.Hash);
                            }
                        }
                        break;
                    }
            }
        }
    }
}
