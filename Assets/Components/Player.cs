using Unity.Entities;
using Unity.NetCode;

public enum PlayerConnectionState : byte
{
    Connected,
    Server,
    Disconnected,
}

public struct Player : IComponentData
{
    [GhostField] public int ConnectionId;
    [GhostField] public PlayerConnectionState ConnectionState;
    [GhostField] public int Team;
    public bool IsCoreComputerSpawned;
}
