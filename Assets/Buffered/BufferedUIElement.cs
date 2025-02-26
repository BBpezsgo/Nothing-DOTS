using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public enum BufferedUIElementType : byte
{
    MIN,
    Label,
    MAX,
}

[BurstCompile]
public struct BufferedUIElement : IBufferElementData
{
    [GhostField] public int Id;
    [GhostField] public BufferedUIElementType Type;
    [GhostField] public int2 Position;
    [GhostField] public int2 Size;
    [GhostField] public FixedString32Bytes Text;
    [GhostField] public float3 Color;

    public BufferedUIElement(int id, BufferedUIElementType type, int2 position, int2 size, FixedString32Bytes text, float3 color)
    {
        Id = id;
        Type = type;
        Position = position;
        Size = size;
        Text = text;
        Color = color;
    }
}
