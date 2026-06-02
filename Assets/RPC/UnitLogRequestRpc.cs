using Unity.NetCode;

struct UnitLogRequestRpc : IRpcCommand
{
    public required SpawnedGhost Ghost;
    public required long From;
    public required int Count;
}

