using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using LanguageCore.Runtime;
using System;
using Unity.Collections;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FacilityProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, facility, transform, localTransform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Facility>, RefRO<LocalToWorld>, RefRO<LocalTransform>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            if (facility.ValueRO.Current.Name.IsEmpty)
            {
                mapped->Facility.Status = 0;
                if (mapped->Facility.HashLocation != 0)
                {
                    Marshal.GetString(Processor.GetMemoryPtr(ref processor.ValueRW), mapped->Facility.HashLocation, out var hash);
                }
            }
            else
            {
                mapped->Facility.Status = 1;
                if (mapped->Facility.HashLocation != 0)
                {
                    Span<byte> bytes = stackalloc byte[32];
                    new Span<byte>(facility.ValueRO.Current.Hash.GetUnsafePtr(), facility.ValueRO.Current.Hash.Length).CopyTo(bytes);
                    bytes[facility.ValueRO.Current.Hash.Length] = 0;
                    Processor.GetMemoryPtr(ref processor.ValueRW).Set(mapped->Facility.HashLocation, bytes[..(facility.ValueRO.Current.Hash.Length + 1)]);
                }
            }
        }
    }
}
