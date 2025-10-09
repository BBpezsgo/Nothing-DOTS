using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct MeshBounds : IComponentData
{
    public Bounds Bounds;
}
