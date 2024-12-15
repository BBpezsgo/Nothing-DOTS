using Unity.Burst;
using Unity.NetCode;

public enum ProcessorCommand : byte
{
    Halt,
    Reset,
    Continue,
    Key,
}

[BurstCompile]
public struct ProcessorCommandRequestRpc : IRpcCommand
{
    public required GhostInstance Entity;
    public required ProcessorCommand Command;
    public required ushort Data;
}
