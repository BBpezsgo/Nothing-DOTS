using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public struct QuadrantEntity
{
    public readonly Entity Entity;
    public readonly Collider Collider;
    public float3 Position;
    public float3 LastPosition;
    public float3 ResolvedOffset;
    public uint Key;
    public uint Layer;

    public QuadrantEntity(
        Entity entity,
        Collider collider,
        float3 position,
        float3 lastPosition,
        uint key,
        uint layer)
    {
        Entity = entity;
        Collider = collider;
        Position = position;
        LastPosition = lastPosition;
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

[BurstCompile]
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

[BurstCompile]
// [UpdateInGroup(typeof(TransformSystemGroup))]
public partial struct QuadrantSystem : ISystem
{
    const int QuadrantCellSize = 20;

    public static Cell ToGrid(float3 worldPosition)
    {
        if (worldPosition.x < 0f) worldPosition.x += -QuadrantCellSize;
        if (worldPosition.z < 0f) worldPosition.z += -QuadrantCellSize;
        return new(
            (int)(worldPosition.x / QuadrantCellSize),
            (int)(worldPosition.z / QuadrantCellSize)
        );
    }

    [BurstCompile]
    public static void ToGrid(in float3 worldPosition, out Cell position)
    {
        float3 fixedWorldPosition = worldPosition;
        if (worldPosition.x < 0f) fixedWorldPosition.x += -QuadrantCellSize;
        if (worldPosition.z < 0f) fixedWorldPosition.z += -QuadrantCellSize;
        position = new(
            (int)(fixedWorldPosition.x / QuadrantCellSize),
            (int)(fixedWorldPosition.z / QuadrantCellSize)
        );
    }

    public static float2 ToGridF(float3 worldPosition) => new(
        math.clamp(worldPosition.x / QuadrantCellSize, short.MinValue, short.MaxValue),
        math.clamp(worldPosition.z / QuadrantCellSize, short.MinValue, short.MaxValue)
    );

    [BurstCompile]
    public static void ToGridF(in float3 worldPosition, out float2 position)
    {
        position = new(
            math.clamp(worldPosition.x / QuadrantCellSize, short.MinValue, short.MaxValue),
            math.clamp(worldPosition.z / QuadrantCellSize, short.MinValue, short.MaxValue)
        );
    }

    public static float3 ToWorld(Cell position) => new(
        position.x * QuadrantCellSize,
        0f,
        position.y * QuadrantCellSize
    );

    [BurstCompile]
    public static void ToWorld(in Cell position, out float3 worldPosition)
    {
        worldPosition = new(
            position.x * QuadrantCellSize,
            0f,
            position.y * QuadrantCellSize
        );
    }

    public static Color CellColor(uint key)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        if (key == uint.MaxValue) return Color.white;
        var random = Unity.Mathematics.Random.CreateFromIndex(key);
        var c = random.NextFloat3();
        return new Color(c.x, c.y, c.z);
#else
        return default;
#endif
    }

    public static void DrawQuadrant(Cell cell)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        float3 start = ToWorld(cell);
        float3 end = start + new float3(QuadrantCellSize, 0f, QuadrantCellSize);
        DebugEx.DrawRectangle(start, end, CellColor(cell.key), .1f);
#endif
    }

    NativeParallelHashMap<uint, NativeList<QuadrantEntity>> HashMap;

    public static NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly GetMap(ref SystemState state) => GetMap(state.WorldUnmanaged);
    public static NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly GetMap(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<QuadrantSystem>();
        QuadrantSystem system = world.GetUnsafeSystemRef<QuadrantSystem>(handle);
        return system.HashMap.AsReadOnly();
    }

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        HashMap = new(128, Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var item in HashMap)
        { item.Value.Clear(); }

        foreach (var (inQuadrant, collider, transform, entity) in
            SystemAPI.Query<RefRW<QuadrantEntityIdentifier>, RefRO<Collider>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            ToGrid(transform.ValueRO.Position, out Cell cell);

            inQuadrant.ValueRW.Added = true;
            inQuadrant.ValueRW.Key = cell.key;
            if (!HashMap.ContainsKey(cell.key))
            { HashMap.Add(cell.key, new(32, Allocator.Persistent)); }
            HashMap[cell.key].Add(new QuadrantEntity(
                entity,
                collider.ValueRO,
                transform.ValueRO.Position,
                default,
                cell.key,
                inQuadrant.ValueRO.Layer));
        }
    }
}
