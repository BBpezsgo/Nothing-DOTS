using System;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

public enum PlayerConnectionState : byte
{
    Connected,
    Server,
    Disconnected,
}

[BurstCompile]
public struct Player : IComponentData
{
    [GhostField] public int ConnectionId;
    [GhostField] public PlayerConnectionState ConnectionState;
    [GhostField] public int Team;
    [GhostField] public float Resources;
    public bool IsCoreComputerSpawned;
    public Guid Guid;
}
