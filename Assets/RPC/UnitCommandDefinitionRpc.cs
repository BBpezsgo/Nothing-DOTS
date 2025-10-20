using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct UnitCommandDefinitionRpc : IRpcCommand
{
    public required FileId FileName;
    public required int Index;
    public required int Id;
    public required FixedString32Bytes Label;
    public required FixedList64Bytes<UnitCommandParameter> Parameters;
}
