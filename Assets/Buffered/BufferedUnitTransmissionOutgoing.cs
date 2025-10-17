using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedUnitTransmissionOutgoing : IBufferElementData
{
    public required float3 Source;
    /// <summary>
    /// Direction in local space
    /// </summary>
    public required float3 Direction;
    public required float Angle;
    public required float CosAngle;
    public required FixedList32Bytes<byte> Data;
}
