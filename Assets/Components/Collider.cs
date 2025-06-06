using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SphereCollider
{
    [MarshalAs(UnmanagedType.U1)] public bool IsEnabled;
    [MarshalAs(UnmanagedType.U1)] public readonly bool IsStatic;
    public readonly float Radius;
    public readonly float3 Offset;

    public SphereCollider(bool isStatic, float radius, float3 offset)
    {
        IsEnabled = true;
        IsStatic = isStatic;
        Radius = radius;
        Offset = offset;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AABBCollider
{
    [MarshalAs(UnmanagedType.U1)] public bool IsEnabled;
    [MarshalAs(UnmanagedType.U1)] public readonly bool IsStatic;
    public readonly AABB AABB;

    public AABBCollider(bool isStatic, AABB aabb)
    {
        IsEnabled = true;
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
public struct Collider : IComponentData
{
    [FieldOffset(0)] public readonly ColliderType Type;

    [FieldOffset(1)] public readonly SphereCollider Sphere;
    [FieldOffset(1)] public readonly AABBCollider AABB;

    [FieldOffset(1), MarshalAs(UnmanagedType.U1)] public bool IsEnabled;
    [FieldOffset(2), MarshalAs(UnmanagedType.U1)] public readonly bool IsStatic;

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
