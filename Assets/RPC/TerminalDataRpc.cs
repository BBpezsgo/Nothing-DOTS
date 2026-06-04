using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct TerminalDataRpc : IRpcCommand
{
    public required SpawnedGhost Entity;
    public required FixedList64Bytes<byte> Data;
    public required ulong Offset;
}
