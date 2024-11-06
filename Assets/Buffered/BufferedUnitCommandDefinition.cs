using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public enum UnitCommandParameter : byte
{
    Position,
}

[BurstCompile]
public struct BufferedUnitCommandDefinition : IBufferElementData
{
    [GhostField] public int Id;
    [GhostField] public FixedString32Bytes Label;
    [GhostField] public FixedList64Bytes<UnitCommandParameter> Parameters;

    public BufferedUnitCommandDefinition(
        int id,
        FixedString32Bytes label,
        FixedList64Bytes<UnitCommandParameter> parameters)
    {
        Id = id;
        Label = label;
        Parameters = parameters;
    }
}
