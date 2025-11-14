using Unity.Entities;
using UnityEngine;

public struct MeshBounds : IComponentData
{
    public Bounds Bounds;
}
