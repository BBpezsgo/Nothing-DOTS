using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct BufferedWorldLabel : IBufferElementData
{
    [GhostField(Quantization = 100)] public float3 Position;
    [GhostField] public byte Color;
    [GhostField] public FixedString32Bytes Text;
    [GhostField(SendData = false)] public float DieAt;

    public BufferedWorldLabel(float3 value, byte color, FixedString32Bytes text, float dieAt)
    {
        Position = value;
        Color = color;
        Text = text;
        DieAt = dieAt;
    }
}
