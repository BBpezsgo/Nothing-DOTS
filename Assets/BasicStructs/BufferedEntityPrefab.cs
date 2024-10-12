using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedEntityPrefab : IBufferElementData
{
    public readonly Entity Entity;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferedEntityPrefab(Entity entity) => Entity = entity;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BufferedEntityPrefab(Entity entity) => new(entity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj) => obj is Entity v1 && Entity.Equals(v1) || obj is BufferedEntityPrefab v2 && Entity.Equals(v2.Entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Entity.GetHashCode();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Entity.ToString();
}
