using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedUnitTransmissionOutgoing : IBufferElementData
{
    /// <summary>
    /// Position in world space
    /// </summary>
    public float3 Source;
    /// <summary>
    /// Direction in world space
    /// </summary>
    public float3 Direction;
    public float Angle;
    public float CosAngle;
    public FixedList32Bytes<byte> Data;

    public BufferedUnitTransmissionOutgoing(float3 source, float3 direction, FixedList32Bytes<byte> data, float cosAngle, float angle)
    {
        Source = source;
        Direction = direction;
        Data = data;
        CosAngle = cosAngle;
        Angle = angle;
    }
}
