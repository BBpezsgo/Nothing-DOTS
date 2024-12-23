using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SphereCollider
{
    public readonly bool IsStatic;
    public readonly float Radius;
    public readonly float3 Offset;

    public SphereCollider(bool isStatic, float radius, float3 offset)
    {
        IsStatic = isStatic;
        Radius = radius;
        Offset = offset;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct AABBCollider
{
    public readonly bool IsStatic;
    public readonly AABB AABB;

    public AABBCollider(bool isStatic, AABB aabb)
    {
        IsStatic = isStatic;
        AABB = aabb;
    }
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
    [FieldOffset(1)] public readonly bool IsStatic;
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

    public static implicit operator Collider(SphereCollider v) => new(v);
    public static implicit operator Collider(AABBCollider v) => new(v);
}
