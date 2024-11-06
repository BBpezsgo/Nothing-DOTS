using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct QuadrantEntity
{
    public readonly Entity Entity;
    public float3 Position;
    public float3 ResolvedOffset;
    public uint Key;

    public QuadrantEntity(Entity entity, float3 position, uint key)
    {
        Entity = entity;
        Position = position;
        ResolvedOffset = default;
        Key = key;
    }

    public override readonly int GetHashCode() => Entity.GetHashCode();
}

public readonly struct Hit
{
    public readonly QuadrantEntity Entity;
    public readonly float Distance;
    public readonly float3 Position;

    public Hit(QuadrantEntity entity, float distance, float3 position)
    {
        Entity = entity;
        Distance = distance;
        Position = position;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct QuadrantSystem : ISystem
{
    const int QuadrantCellSize = 20;

    public static int2 ToGrid(float3 position) => new(
         Math.Clamp((int)(position.x / QuadrantCellSize), short.MinValue, short.MaxValue),
         Math.Clamp((int)(position.z / QuadrantCellSize), short.MinValue, short.MaxValue)
     );

    public static float3 ToWorld(int2 position) => new(
        position.x * QuadrantCellSize,
        0f,
        position.y * QuadrantCellSize
    );

    public static uint GetKey(int2 cell) => unchecked((uint)(
          ((cell.x & 0xFFFF) << 16) |
          ((cell.y & 0xFFFF) << 00)
      ));

    public static int2 GetCell(uint key) => new(
        unchecked((short)((key >> 16) & 0xFFFF)),
        unchecked((short)((key >> 00) & 0xFFFF))
    );

    NativeHashMap<uint, NativeList<QuadrantEntity>> HashMap;

    public static NativeHashMap<uint, NativeList<QuadrantEntity>> GetMap(ref SystemState state) => GetMap(state.WorldUnmanaged);
    public static NativeHashMap<uint, NativeList<QuadrantEntity>> GetMap(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<QuadrantSystem>();
        QuadrantSystem system = world.GetUnsafeSystemRef<QuadrantSystem>(handle);
        return system.HashMap;
    }

    void ISystem.OnCreate(ref SystemState state)
    {
        HashMap = new(128, Allocator.Persistent);
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (inQuadrant, transform, entity) in
            SystemAPI.Query<RefRW<QuadrantEntityIdentifier>, RefRO<LocalToWorld>>()
            .WithEntityAccess())
        {
            uint key = GetKey(ToGrid(transform.ValueRO.Position));

            QuadrantEntity item = new(entity, transform.ValueRO.Position, key);

            if (inQuadrant.ValueRO.Added)
            {
                NativeList<QuadrantEntity> list = HashMap[inQuadrant.ValueRO.Key];
                if (key == inQuadrant.ValueRO.Key)
                {
                    for (int j = 0; j < list.Length; j++)
                    {
                        if (list[j].Entity == entity)
                        {
                            list[j] = item;
                            break;
                        }
                    }
                    continue;
                }

                for (int j = 0; j < list.Length; j++)
                {
                    if (list[j].Entity == entity)
                    {
                        list.RemoveAt(j);
                        break;
                    }
                }
            }

            inQuadrant.ValueRW.Added = true;
            inQuadrant.ValueRW.Key = key;
            HashMap.TryAdd(key, new(Allocator.Persistent));
            HashMap[key].Add(item);
        }
    }
}
