using Unity.NetCode;

struct StopDebugRequestRpc : IRpcCommand
{
    public required SpawnedGhost Entity;
}

