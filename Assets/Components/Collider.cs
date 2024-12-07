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
    [FieldOffset(0)] public readonly bool IsStatic;
    [FieldOffset(1)] public readonly ColliderType Type;
    [FieldOffset(2)] public readonly SphereCollider Sphere;
    [FieldOffset(2)] public readonly AABBCollider AABB;

    public Collider(bool isStatic, SphereCollider sphere) : this()
    {
        IsStatic = isStatic;
        Type = ColliderType.Sphere;
        Sphere = sphere;
    }

    public Collider(bool isStatic, AABBCollider aabb) : this()
    {
        IsStatic = isStatic;
        Type = ColliderType.AABB;
        AABB = aabb;
    }
}
