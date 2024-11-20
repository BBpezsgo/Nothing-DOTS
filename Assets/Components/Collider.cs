using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

public struct SphereCollider
{
    public float Radius;
}

public struct AABBCollider
{
    public AABB AABB;
}

public enum ColliderType : byte
{
    Sphere,
    AABB,
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct Collider : IComponentData
{
    [FieldOffset(0)] public readonly ColliderType Type;
    [FieldOffset(1)] public readonly SphereCollider Sphere;
    [FieldOffset(1)] public readonly AABBCollider AABB;

    public Collider(SphereCollider sphere) : this()
    {
        Type = ColliderType.Sphere;
        Sphere = sphere;
    }

    public Collider(AABBCollider aabb) : this()
    {
        Type = ColliderType.AABB;
        AABB = aabb;
    }
}
