using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public enum PlayerConnectionState : byte
{
    Connected,
    Server,
    Disconnected,
}

public enum GameOutcome
{
    None,
    Won,
    Lost,
}

[BurstCompile]
public struct Player : IComponentData
{
    [GhostField] public int ConnectionId;
    [GhostField] public PlayerConnectionState ConnectionState;
    [GhostField] public int Team;
    [GhostField] public float Resources;
    [GhostField] public FixedString32Bytes Nickname;
    [GhostField] public GameOutcome Outcome;
    public bool IsCoreComputerSpawned;
    public Guid Guid;
    public float2 Position;
}
