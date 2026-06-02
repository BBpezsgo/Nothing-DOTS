using Unity.Collections;
using Unity.NetCode;

struct UnitLogResponseRpc : IRpcCommand
{
    public required SpawnedGhost Ghost;
    public required long From;
    public required long To;
    public required int Count;
    public required FixedList512Bytes<byte> Data;
}

