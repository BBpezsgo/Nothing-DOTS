using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct BufferedWire : IBufferElementData
{
    [GhostField(SendData = false)] public Entity EntityA;
    [GhostField(SendData = false)] public Entity EntityB;
    [GhostField] public SpawnedGhost GhostA;
    [GhostField] public SpawnedGhost GhostB;
}
