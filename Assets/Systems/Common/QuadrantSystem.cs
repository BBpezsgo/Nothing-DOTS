using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct QuadrantEntity
{
    public readonly Entity Entity;
    public float3 Position;
    public float3 ResolvedOffset;
    public uint Key;
    public uint Layer;

    public QuadrantEntity(Entity entity, float3 position, uint key, uint layer)
    {
        Entity = entity;
        Position = position;
        ResolvedOffset = default;
        Key = key;
        Layer = layer;
    }

    public override readonly int GetHashCode() => Entity.GetHashCode();
}

[BurstCompile]
[StructLayout(LayoutKind.Explicit)]
public struct Cell : IEquatable<Cell>
{
    [FieldOffset(0)]
    public uint key;

    [FieldOffset(0)]
    public short x;
    [FieldOffset(2)]
    public short y;

    public Cell(uint key)
    {
        this.key = key;
    }

    public Cell(short x, short y)
    {
        this.x = x;
        this.y = y;
    }

    public Cell(int x, int y)
    {
        this.x = (short)math.clamp(x, short.MinValue, short.MaxValue);
        this.y = (short)math.clamp(y, short.MinValue, short.MaxValue);
    }

    public static Cell operator +(Cell a, Cell b) => new(a.x + b.x, a.y + b.y);
    public static Cell operator -(Cell a, Cell b) => new(a.x - b.x, a.y - b.y);

    [BurstCompile] public override readonly int GetHashCode() => unchecked((int)key);
    public override readonly bool Equals(object obj) => obj is Cell other && Equals(other);
    public readonly bool Equals(Cell other) => key == other.key;
    public override readonly string ToString() => $"Cell({x}, {y})";
}

public readonly struct Hit
{
    public readonly QuadrantEntity Entity;
    public readonly float Distance;

    public Hit(QuadrantEntity entity, float distance)
    {
        Entity = entity;
        Distance = distance;
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
public partial struct QuadrantSystem : ISystem
{
    const int QuadrantCellSize = 20;

    public static Cell ToGrid(float3 position)
    {
        if (position.x < 0f) position.x += -QuadrantCellSize;
        if (position.z < 0f) position.z += -QuadrantCellSize;
        return new(
            (int)(position.x / QuadrantCellSize),
            (int)(position.z / QuadrantCellSize)
        );
    }

    public static float2 ToGridF(float3 position) => new(
        math.clamp(position.x / QuadrantCellSize, short.MinValue, short.MaxValue),
        math.clamp(position.z / QuadrantCellSize, short.MinValue, short.MaxValue)
    );

    public static float3 ToWorld(Cell position) => new(
        position.x * QuadrantCellSize,
        0f,
        position.y * QuadrantCellSize
    );

    public static Color CellColor(uint key)
    {
        if (key == uint.MaxValue) return Color.white;
        var random = Unity.Mathematics.Random.CreateFromIndex(key);
        var c = random.NextFloat3();
        return new Color(c.x, c.y, c.z);
    }

    public static void DrawQuadrant(Cell cell)
    {
        float3 start = ToWorld(cell);
        float3 end = start + new float3(QuadrantCellSize, 0f, QuadrantCellSize);
        DebugEx.DrawRectangle(start, end, CellColor(cell.key), .1f);
    }

    NativeParallelHashMap<uint, NativeList<QuadrantEntity>> HashMap;

    public static NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly GetMap(ref SystemState state) => GetMap(state.WorldUnmanaged);
    public static NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly GetMap(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<QuadrantSystem>();
        QuadrantSystem system = world.GetUnsafeSystemRef<QuadrantSystem>(handle);
        return system.HashMap.AsReadOnly();
    }

    void ISystem.OnCreate(ref SystemState state)
    {
        HashMap = new(128, Allocator.Persistent);
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var item in HashMap)
        { item.Value.Clear(); }

        foreach (var (inQuadrant, transform, entity) in
            SystemAPI.Query<RefRW<QuadrantEntityIdentifier>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            Cell cell = ToGrid(transform.ValueRO.Position);

            inQuadrant.ValueRW.Added = true;
            inQuadrant.ValueRW.Key = cell.key;
            if (!HashMap.ContainsKey(cell.key))
            { HashMap.Add(cell.key, new(32, Allocator.Persistent)); }
            HashMap[cell.key].Add(new QuadrantEntity(entity, transform.ValueRO.Position, cell.key, inQuadrant.ValueRO.Layer));
        }
    }
}
