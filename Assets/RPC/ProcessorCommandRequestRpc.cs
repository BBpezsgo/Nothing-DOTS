using Unity.Burst;
using Unity.NetCode;

public enum ProcessorCommand
{
    Halt,
    Reset,
    Continue,
}

[BurstCompile]
public struct ProcessorCommandRequestRpc : IRpcCommand
{
    public required GhostInstance Entity;
    public required ProcessorCommand Command;
}
