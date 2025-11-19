using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct GhostChild : IComponentData
{
    [GhostField] public SpawnedGhost ParentEntity;
    [GhostField] public float3 LocalPosition;
    [GhostField] public quaternion LocalRotation;
    public SpawnedGhost LocalParentEntity;
}
