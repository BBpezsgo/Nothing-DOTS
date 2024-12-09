using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct BufferedLine : IBufferElementData
{
    [GhostField(Quantization = 100)] public float3x2 Value;
    [GhostField] public byte Color;
    [GhostField(SendData = false)] public float DieAt;

    public BufferedLine(float3x2 value, byte color, float dieAt)
    {
        Value = value;
        Color = color;
        DieAt = dieAt;
    }
}
