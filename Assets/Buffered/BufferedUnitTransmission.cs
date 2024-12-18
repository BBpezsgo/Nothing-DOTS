using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedUnitTransmission : IBufferElementData
{
    /// <summary>
    /// Position in world space
    /// </summary>
    public required float3 Source;
    public required FixedList32Bytes<byte> Data;
}
