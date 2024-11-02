using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedUnitTransmissionOutgoing : IBufferElementData
{
    public float3 Source;
    public float3 Direction;
    public float CosAngle;
    public FixedList32Bytes<byte> Data;

    public BufferedUnitTransmissionOutgoing(float3 source, float3 direction, FixedList32Bytes<byte> data, float cosAngle)
    {
        Source = source;
        Direction = direction;
        Data = data;
        CosAngle = cosAngle;
    }
}
