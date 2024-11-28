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
    public float3 Source;
    public FixedList32Bytes<byte> Data;

    public BufferedUnitTransmission(float3 source, FixedList32Bytes<byte> data)
    {
        Source = source;
        Data = data;
    }
}
