using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public enum UnitCommandParameter : byte
{
    Position2,
    Position3,
}

[BurstCompile]
public struct BufferedUnitCommandDefinition : IBufferElementData
{
    [GhostField] public int Id;
    [GhostField] public FixedString32Bytes Label;
    [GhostField] public ushort ParameterCount;
    [GhostField] public FixedBytes62 Parameters;

    public unsafe UnitCommandParameter GetParameter(int index) => ((UnitCommandParameter*)Unsafe.AsPointer(ref Parameters))[index];

    public unsafe ReadOnlySpan<UnitCommandParameter> GetParameters() => new(Unsafe.AsPointer(ref Parameters), ParameterCount);

    public unsafe BufferedUnitCommandDefinition(
        int id,
        FixedString32Bytes label,
        FixedList64Bytes<UnitCommandParameter> parameters)
    {
        Id = id;
        Label = label;
        ParameterCount = (ushort)parameters.Length;
        FixedBytes62 Parameters = default;
        for (int i = 0; i < parameters.Length; i++)
        {
            ((byte*)&Parameters)[i] = (byte)parameters[i];
        }
        this.Parameters = Parameters;
    }

    public override readonly int GetHashCode() => Id;
}
