using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedTransmittedUnitData : IBufferElementData
{
    public float3 Source;
    public FixedList32Bytes<byte> Data;

    public BufferedTransmittedUnitData(float3 source, FixedList32Bytes<byte> data)
    {
        Source = source;
        Data = data;
    }
}
