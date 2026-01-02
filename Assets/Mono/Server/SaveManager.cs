using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BinaryReader = Unity.Entities.Serialization.BinaryReader;
using BinaryWriter = Unity.Entities.Serialization.BinaryWriter;

static unsafe class FixedListExtensions
{
    public static Span<T> AsSpan<T>(ref this FixedList32Bytes<T> list) where T : unmanaged => new(list.GetUnsafePtr(), list.Length);
    public static Span<T> AsSpan<T>(ref this FixedList64Bytes<T> list) where T : unmanaged => new(list.GetUnsafePtr(), list.Length);
    public static Span<T> AsSpan<T>(ref this FixedList128Bytes<T> list) where T : unmanaged => new(list.GetUnsafePtr(), list.Length);
    public static Span<T> AsSpan<T>(ref this FixedList512Bytes<T> list) where T : unmanaged => new(list.GetUnsafePtr(), list.Length);
    public static Span<T> AsSpan<T>(ref this FixedList4096Bytes<T> list) where T : unmanaged => new(list.GetUnsafePtr(), list.Length);

    public static T* GetUnsafePtr<T>(ref this FixedList32Bytes<T> list) where T : unmanaged => (T*)((byte*)Unsafe.AsPointer(ref list) + UnsafeUtility.SizeOf<ushort>());
    public static T* GetUnsafePtr<T>(ref this FixedList64Bytes<T> list) where T : unmanaged => (T*)((byte*)Unsafe.AsPointer(ref list) + UnsafeUtility.SizeOf<ushort>());
    public static T* GetUnsafePtr<T>(ref this FixedList128Bytes<T> list) where T : unmanaged => (T*)((byte*)Unsafe.AsPointer(ref list) + UnsafeUtility.SizeOf<ushort>());
    public static T* GetUnsafePtr<T>(ref this FixedList512Bytes<T> list) where T : unmanaged => (T*)((byte*)Unsafe.AsPointer(ref list) + UnsafeUtility.SizeOf<ushort>());
    public static T* GetUnsafePtr<T>(ref this FixedList4096Bytes<T> list) where T : unmanaged => (T*)((byte*)Unsafe.AsPointer(ref list) + UnsafeUtility.SizeOf<ushort>());
}

static unsafe partial class FixedStringExtensions
{
    public static Span<byte> AsSpan(ref this FixedString32Bytes list) => new(list.GetUnsafePtr(), list.Length);
    public static Span<byte> AsSpan(ref this FixedString64Bytes list) => new(list.GetUnsafePtr(), list.Length);
    public static Span<byte> AsSpan(ref this FixedString128Bytes list) => new(list.GetUnsafePtr(), list.Length);
    public static Span<byte> AsSpan(ref this FixedString512Bytes list) => new(list.GetUnsafePtr(), list.Length);
    public static Span<byte> AsSpan(ref this FixedString4096Bytes list) => new(list.GetUnsafePtr(), list.Length);
}

static unsafe class BinaryWriterExtensions
{
    public static void Write(this BinaryWriter writer, Guid value) => writer.Write(value.ToByteArray());
    public static void WriteUnsafe<T>(this BinaryWriter writer, T value) where T : unmanaged => writer.WriteBytes(Unsafe.AsPointer(ref value), UnsafeUtility.SizeOf<T>());
    public static void Write(this BinaryWriter writer, long value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, ulong value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, int value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, uint value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, short value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, ushort value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, char value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, sbyte value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, byte value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, bool value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, float value) => writer.WriteUnsafe(value);
    public static void Write(this BinaryWriter writer, float2 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
    }
    public static void Write(this BinaryWriter writer, float3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }
    public static void Write(this BinaryWriter writer, float4 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
        writer.Write(value.w);
    }
    public static void Write(this BinaryWriter writer, quaternion value) => writer.Write(value.value);

    [SuppressMessage("Style", "IDE0010:Add missing cases")]
    public static void Write<T>(this BinaryWriter writer, T value) where T : unmanaged, Enum
    {
        switch (value.GetTypeCode())
        {
            case TypeCode.Boolean:
                writer.WriteBytes(Unsafe.AsPointer(ref value), UnsafeUtility.SizeOf<bool>());
                break;
            case TypeCode.Byte:
                writer.WriteBytes(Unsafe.AsPointer(ref value), UnsafeUtility.SizeOf<byte>());
                break;
            case TypeCode.Char:
                writer.WriteBytes(Unsafe.AsPointer(ref value), UnsafeUtility.SizeOf<char>());
                break;
            case TypeCode.Int32:
                writer.WriteBytes(Unsafe.AsPointer(ref value), UnsafeUtility.SizeOf<int>());
                break;
            default:
                throw new NotImplementedException(value.GetTypeCode().ToString());
        }
    }

    public static void WriteUnsafe<T>(this BinaryWriter writer, FixedList32Bytes<T> value) where T : unmanaged
    {
        writer.Write((byte)value.Length);
        writer.WriteBytes(value.GetUnsafePtr(), value.Length * UnsafeUtility.SizeOf<T>());
    }
    public static void WriteUnsafe<T>(this BinaryWriter writer, FixedList64Bytes<T> value) where T : unmanaged
    {
        writer.Write((byte)value.Length);
        writer.WriteBytes(value.GetUnsafePtr(), value.Length * UnsafeUtility.SizeOf<T>());
    }
    public static void WriteUnsafe<T>(this BinaryWriter writer, FixedList128Bytes<T> value) where T : unmanaged
    {
        writer.Write((byte)value.Length);
        writer.WriteBytes(value.GetUnsafePtr(), value.Length * UnsafeUtility.SizeOf<T>());
    }
    public static void WriteUnsafe<T>(this BinaryWriter writer, FixedList512Bytes<T> value) where T : unmanaged
    {
        writer.Write((ushort)value.Length);
        writer.WriteBytes(value.GetUnsafePtr(), value.Length * UnsafeUtility.SizeOf<T>());
    }
    public static void WriteUnsafe<T>(this BinaryWriter writer, FixedList4096Bytes<T> value) where T : unmanaged
    {
        writer.Write((ushort)value.Length);
        writer.WriteBytes(value.GetUnsafePtr(), value.Length * UnsafeUtility.SizeOf<T>());
    }

    public static void Write(this BinaryWriter writer, FixedString32Bytes value) => writer.WriteUnsafe(value.AsFixedList());
    public static void Write(this BinaryWriter writer, FixedString64Bytes value) => writer.WriteUnsafe(value.AsFixedList());
    public static void Write(this BinaryWriter writer, FixedString128Bytes value) => writer.WriteUnsafe(value.AsFixedList());
    public static void Write(this BinaryWriter writer, FixedString512Bytes value) => writer.WriteUnsafe(value.AsFixedList());
    public static void Write(this BinaryWriter writer, FixedString4096Bytes value) => writer.WriteUnsafe(value.AsFixedList());

    public delegate void ItemSerializer<T>(BinaryWriter writer, T item);

    public static void Write<T>(this BinaryWriter writer, DynamicBuffer<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values.ElementAt(i));
        }
    }
    public static void Write<T>(this BinaryWriter writer, IReadOnlyCollection<T> values, ItemSerializer<T> serializer)
    {
        writer.Write(values.Count);
        foreach (T item in values)
        {
            serializer(writer, item);
        }
    }
    public static void Write<T>(this BinaryWriter writer, UnsafeList<T>.ReadOnly values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values.Ptr[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, UnsafeList<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values.Ptr[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, ReadOnlySpan<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, FixedList32Bytes<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write((byte)values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, FixedList64Bytes<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write((byte)values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, FixedList128Bytes<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write((byte)values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, FixedList512Bytes<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write((ushort)values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
    public static void Write<T>(this BinaryWriter writer, FixedList4096Bytes<T> values, ItemSerializer<T> serializer) where T : unmanaged
    {
        writer.Write((ushort)values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            serializer(writer, values[i]);
        }
    }
}

static unsafe class BinaryReaderExtensions
{
    public static byte[] ReadBytes(this BinaryReader reader, int length)
    {
        byte[] res = new byte[length];
        fixed (byte* ptr = res)
        {
            reader.ReadBytes(ptr, length);
        }
        return res;
    }

    public static Guid ReadGuid(this BinaryReader reader) => new(reader.ReadBytes(16));
    public static T ReadUnsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        T res;
        reader.ReadBytes(&res, UnsafeUtility.SizeOf<T>());
        return res;
    }
    public static long ReadLong(this BinaryReader reader) => reader.ReadUnsafe<long>();
    public static ulong ReadUlong(this BinaryReader reader) => reader.ReadUnsafe<ulong>();
    public static int ReadInt(this BinaryReader reader) => reader.ReadUnsafe<int>();
    public static uint ReadUint(this BinaryReader reader) => reader.ReadUnsafe<uint>();
    public static short ReadShort(this BinaryReader reader) => reader.ReadUnsafe<short>();
    public static ushort ReadUshort(this BinaryReader reader) => reader.ReadUnsafe<ushort>();
    public static char ReadChar(this BinaryReader reader) => reader.ReadUnsafe<char>();
    public static sbyte ReadSbyte(this BinaryReader reader) => reader.ReadUnsafe<sbyte>();
    public static byte ReadByte(this BinaryReader reader) => reader.ReadUnsafe<byte>();
    public static bool ReadBool(this BinaryReader reader) => reader.ReadUnsafe<bool>();
    public static float ReadFloat(this BinaryReader reader) => reader.ReadUnsafe<float>();
    public static float2 ReadFloat2(this BinaryReader reader) => new(reader.ReadFloat(), reader.ReadFloat());
    public static float3 ReadFloat3(this BinaryReader reader) => new(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    public static float4 ReadFloat4(this BinaryReader reader) => new(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    public static quaternion ReadQuaternion(this BinaryReader reader) => new(reader.ReadFloat4());

    public static FixedList32Bytes<T> ReadFixedList32Unsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        FixedList32Bytes<T> res = new() { Length = reader.ReadByte() };
        reader.ReadBytes(res.GetUnsafePtr(), res.Length * UnsafeUtility.SizeOf<T>());
        return res;
    }
    public static FixedList64Bytes<T> ReadFixedList64Unsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        FixedList64Bytes<T> res = new() { Length = reader.ReadByte() };
        reader.ReadBytes(res.GetUnsafePtr(), res.Length * UnsafeUtility.SizeOf<T>());
        return res;
    }
    public static FixedList128Bytes<T> ReadFixedList128Unsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        FixedList128Bytes<T> res = new() { Length = reader.ReadByte() };
        reader.ReadBytes(res.GetUnsafePtr(), res.Length * UnsafeUtility.SizeOf<T>());
        return res;
    }
    public static FixedList512Bytes<T> ReadFixedList512Unsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        FixedList512Bytes<T> res = new() { Length = reader.ReadUshort() };
        reader.ReadBytes(res.GetUnsafePtr(), res.Length * UnsafeUtility.SizeOf<T>());
        return res;
    }
    public static FixedList4096Bytes<T> ReadFixedList4096Unsafe<T>(this BinaryReader reader) where T : unmanaged
    {
        FixedList4096Bytes<T> res = new() { Length = reader.ReadUshort() };
        reader.ReadBytes(res.GetUnsafePtr(), res.Length * UnsafeUtility.SizeOf<T>());
        return res;
    }

    public static FixedString32Bytes ReadFixedString32(this BinaryReader reader)
    {
        FixedList32Bytes<byte> data = reader.ReadFixedList32Unsafe<byte>();
        FixedString32Bytes res = new() { Length = data.Length };
        data.AsSpan().CopyTo(res.AsSpan());
        return res;
    }
    public static FixedString64Bytes ReadFixedString64(this BinaryReader reader)
    {
        FixedList64Bytes<byte> data = reader.ReadFixedList64Unsafe<byte>();
        FixedString64Bytes res = new() { Length = data.Length };
        data.AsSpan().CopyTo(res.AsSpan());
        return res;
    }
    public static FixedString128Bytes ReadFixedString128(this BinaryReader reader)
    {
        FixedList128Bytes<byte> data = reader.ReadFixedList128Unsafe<byte>();
        FixedString128Bytes res = new() { Length = data.Length };
        data.AsSpan().CopyTo(res.AsSpan());
        return res;
    }
    public static FixedString512Bytes ReadFixedString512(this BinaryReader reader)
    {
        FixedList512Bytes<byte> data = reader.ReadFixedList512Unsafe<byte>();
        FixedString512Bytes res = new() { Length = data.Length };
        data.AsSpan().CopyTo(res.AsSpan());
        return res;
    }
    public static FixedString4096Bytes ReadFixedString4096(this BinaryReader reader)
    {
        FixedList4096Bytes<byte> data = reader.ReadFixedList4096Unsafe<byte>();
        FixedString4096Bytes res = new() { Length = data.Length };
        data.AsSpan().CopyTo(res.AsSpan());
        return res;
    }

    public delegate T ItemDeserializer<T>(BinaryReader reader);
    public delegate void ItemDeserializerRef<T>(BinaryReader reader, ref T item);

    public static void ReadDynamicBuffer<T>(this BinaryReader reader, DynamicBuffer<T> buffer, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        int length = reader.ReadInt();
        buffer.Length = length;

        for (int i = 0; i < length; i++)
        {
            buffer.ElementAt(i) = deserializer(reader);
        }
    }

    public static void ReadDynamicBuffer<T>(this BinaryReader reader, DynamicBuffer<T> buffer, ItemDeserializerRef<T> deserializer) where T : unmanaged
    {
        int length = reader.ReadInt();
        if (buffer.Length != length) Debug.LogWarning($"Dynamic buffer size changed");

        for (int i = 0; i < length; i++)
        {
            deserializer(reader, ref buffer.ElementAt(i));
        }
    }

    public static T[] ReadArray<T>(this BinaryReader reader, ItemDeserializer<T> deserializer)
    {
        T[] result = new T[reader.ReadInt()];
        for (int i = 0; i < result.Length; i++) result[i] = deserializer(reader);
        return result;
    }

    public static FixedList32Bytes<T> ReadFixedList32<T>(this BinaryReader reader, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        FixedList32Bytes<T> res = new() { Length = reader.ReadByte() };
        for (int i = 0; i < res.Length; i++)
        {
            res[i] = deserializer(reader);
        }
        return res;
    }
    public static FixedList64Bytes<T> ReadFixedList64<T>(this BinaryReader reader, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        FixedList64Bytes<T> res = new() { Length = reader.ReadByte() };
        for (int i = 0; i < res.Length; i++)
        {
            res[i] = deserializer(reader);
        }
        return res;
    }
    public static FixedList128Bytes<T> ReadFixedList128<T>(this BinaryReader reader, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        FixedList128Bytes<T> res = new() { Length = reader.ReadByte() };
        for (int i = 0; i < res.Length; i++)
        {
            res[i] = deserializer(reader);
        }
        return res;
    }
    public static FixedList512Bytes<T> ReadFixedList512<T>(this BinaryReader reader, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        FixedList512Bytes<T> res = new() { Length = reader.ReadUshort() };
        for (int i = 0; i < res.Length; i++)
        {
            res[i] = deserializer(reader);
        }
        return res;
    }
    public static FixedList4096Bytes<T> ReadFixedList4096<T>(this BinaryReader reader, ItemDeserializer<T> deserializer) where T : unmanaged
    {
        FixedList4096Bytes<T> res = new() { Length = reader.ReadUshort() };
        for (int i = 0; i < res.Length; i++)
        {
            res[i] = deserializer(reader);
        }
        return res;
    }
}

class FileBinaryReader : BinaryReader
{
    readonly FileStream fileStream;

    public FileBinaryReader(string path)
    {
        fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
    }

    public long Position { get => fileStream.Position; set => fileStream.Position = value; }

    public void Dispose() => fileStream.Dispose();
    public unsafe void ReadBytes(void* data, int bytes)
    {
        int read = fileStream.Read(new Span<byte>(data, bytes));
        if (read != bytes) throw new EndOfStreamException($"{read} != {bytes}");
    }
}

class FileBinaryWriter : BinaryWriter
{
    readonly FileStream fileStream;

    public FileBinaryWriter(string path)
    {
        fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
    }

    public long Position { get => fileStream.Position; set => fileStream.Position = value; }

    public void Dispose() => fileStream.Dispose();
    public unsafe void WriteBytes(void* data, int bytes) => fileStream.Write(new ReadOnlySpan<byte>(data, bytes));
}

class SaveManager : MonoBehaviour
{
    readonly struct TypeSerializer
    {
        public readonly ComponentType Type;
        public readonly BasicSerializer Serializer;
        public readonly BasicDeserializer Deserializer;

        public delegate void BasicSerializer(BinaryWriter writer, Entity entity, EntityManager entityManager);
        public delegate void BasicDeserializer(BinaryReader reader, Entity entity, EntityManager entityManager);

        public delegate void ComponentSerializer<T>(BinaryWriter writer, T component);
        public delegate void ComponentDeserializer<T>(BinaryReader reader, ref T component);
        public delegate T ComponentDeserializerSimple<T>(BinaryReader reader);

        public TypeSerializer(ComponentType type, BasicSerializer serializer, BasicDeserializer deserializer)
        {
            Type = type;
            Serializer = serializer;
            Deserializer = deserializer;
        }

        public static TypeSerializer Simple<T>(BasicSerializer serializer, BasicDeserializer deserializer) => new(typeof(T), serializer, deserializer);

        public static TypeSerializer ForComponentSimple<T>(ComponentSerializer<T> serializer, ComponentDeserializerSimple<T> deserializer) where T : unmanaged, IComponentData
        {
            if (((ComponentType)typeof(T)).IsZeroSized)
            {
                return new TypeSerializer(
                    typeof(T),
                    (writer, entity, entityManager) => serializer(writer, default),
                    (reader, entity, entityManager) => deserializer(reader)
                );
            }
            else
            {
                return new TypeSerializer(
                    typeof(T),
                    (writer, entity, entityManager) => serializer(writer, entityManager.GetComponentData<T>(entity)),
                    (reader, entity, entityManager) => entityManager.SetComponentData(entity, deserializer(reader))
                );
            }
        }

        public static TypeSerializer ForComponent<T>(ComponentSerializer<T> serializer, ComponentDeserializer<T> deserializer) where T : unmanaged, IComponentData
        {
            if (((ComponentType)typeof(T)).IsZeroSized) throw new UnreachableException();
            return new TypeSerializer(
                typeof(T),
                (writer, entity, entityManager) =>
                {
                    serializer(writer, entityManager.GetComponentData<T>(entity));
                },
                (reader, entity, entityManager) =>
                {
                    T v = entityManager.GetComponentData<T>(entity);
                    deserializer(reader, ref v);
                    entityManager.SetComponentData(entity, v);
                }
            );
        }
    }

    readonly struct PrefabIdSerializer
    {
        public readonly PrefabIdentifier Is;
        public readonly BasicSerializer Serializer;
        public readonly BasicDeserializer Deserializer;

        public delegate bool PrefabIdentifier(ComponentType componentType);

        public delegate void BasicSerializer(BinaryWriter writer, Entity entity, EntityManager entityManager);
        public delegate void ComponentSerializer<T>(BinaryWriter writer, T component);
        public delegate Entity BasicDeserializer(BinaryReader reader, EntityManager entityManager);

        public PrefabIdSerializer(PrefabIdentifier prefabIdentifier, BasicSerializer serializer, BasicDeserializer deserializer)
        {
            Is = prefabIdentifier;
            Serializer = serializer;
            Deserializer = deserializer;
        }

        public static PrefabIdSerializer Simple(PrefabIdentifier prefabIdentifier, BasicSerializer serializer, BasicDeserializer deserializer) => new(prefabIdentifier, serializer, deserializer);

        public static PrefabIdSerializer ForComponent<T>(ComponentSerializer<T> serializer, BasicDeserializer deserializer) where T : unmanaged, IComponentData
        {
            if (((ComponentType)typeof(T)).IsZeroSized)
            {
                return new(
                    static (componentType) => componentType.Equals((ComponentType)typeof(T)),
                    (writer, entity, entityManager) => serializer(writer, default),
                    deserializer
                );
            }
            else
            {
                return new(
                    static (componentType) => componentType.Equals((ComponentType)typeof(T)),
                    (writer, entity, entityManager) => serializer(writer, entityManager.GetComponentData<T>(entity)),
                    deserializer
                );
            }
        }
    }

    static DynamicBuffer<TBufferedItem> GetSingletonBuffer<TSingleton, TBufferedItem>(EntityManager entityManager, bool isReadOnly = false)
        where TSingleton : unmanaged, IComponentData
        where TBufferedItem : unmanaged, IBufferElementData
    {
        using EntityQuery query = entityManager.CreateEntityQuery(typeof(TSingleton));
        Entity entity = query.GetSingletonEntity();
        return entityManager.GetBuffer<TBufferedItem>(entity, isReadOnly);
    }

    static TSingleton GetSingleton<TSingleton>(EntityManager entityManager)
        where TSingleton : unmanaged, IComponentData
    {
        using EntityQuery query = entityManager.CreateEntityQuery(typeof(TSingleton));
        return query.GetSingleton<TSingleton>();
    }

    static unsafe List<TypeSerializer> GetSerializers(EntityManager entityManager)
    {
        DynamicBuffer<BufferedBuilding> buildings = GetSingletonBuffer<BuildingDatabase, BufferedBuilding>(entityManager, false);
        DynamicBuffer<BufferedUnit> units = GetSingletonBuffer<UnitDatabase, BufferedUnit>(entityManager, false);
        DynamicBuffer<BufferedProjectile> projectiles = GetSingletonBuffer<ProjectileDatabase, BufferedProjectile>(entityManager, false);
        PrefabDatabase prefabs = GetSingleton<PrefabDatabase>(entityManager);
        NativeArray<Research>.ReadOnly researches;
        {
            EntityQuery q = entityManager.CreateEntityQuery(typeof(Research));
            researches = q.ToComponentDataArray<Research>(Allocator.Temp).AsReadOnly();
            q.Dispose();
        }

        return new()
        {
            TypeSerializer.ForComponentSimple<LocalTransform>(
                (writer, v) =>
                {
                    writer.Write(v.Position);
                    writer.Write(v.Rotation);
                },
                (reader) =>
                {
                    return LocalTransform.FromPositionRotation(reader.ReadFloat3(), reader.ReadQuaternion());
                }
            ),
            TypeSerializer.ForComponentSimple<BuildingPrefabInstance>(
                (writer, v) =>
                {
                    writer.Write(v.Index);
                },
                (reader) =>
                {
                    return new BuildingPrefabInstance()
                    {
                        Index = reader.ReadInt(),
                    };
                }
            ),
            TypeSerializer.ForComponentSimple<UnitPrefabInstance>(
                (writer, v) =>
                {
                    writer.Write(v.Index);
                },
                (reader) =>
                {
                    return new UnitPrefabInstance()
                    {
                        Index = reader.ReadInt(),
                    };
                }
            ),
            TypeSerializer.ForComponentSimple<UnitTeam>(
                (writer, v) =>
                {
                    writer.Write(v.Team);
                },
                (reader) =>
                {
                    return new UnitTeam()
                    {
                        Team = reader.ReadInt(),
                    };
                }
            ),
            TypeSerializer.ForComponentSimple<Vehicle>(
                (writer, v) =>
                {
                    writer.Write(v.Input);
                    writer.Write(v.Speed);
                },
                (reader) =>
                {
                    return new Vehicle()
                    {
                        Input = reader.ReadFloat2(),
                        Speed = reader.ReadFloat(),
                    };
                }
            ),
            TypeSerializer.ForComponent<Rigidbody>(
                (writer, v) =>
                {
                    writer.Write(v.Velocity);
                    writer.Write(v.IsEnabled);
                },
                (BinaryReader reader, ref Rigidbody v) =>
                {
                    v.Velocity = reader.ReadFloat3();
                    v.IsEnabled = reader.ReadBool();
                }
            ),
            TypeSerializer.ForComponentSimple<Facility>(
                (writer, v) =>
                {
                    writer.Write(v.Current.Name);
                    writer.Write(v.CurrentProgress);
                },
                (reader) =>
                {
                    FixedString64Bytes name = reader.ReadFixedString64();
                    int i = researches.IndexOf(v => v.Name == name);
                    if (i == -1)
                    {
                        Debug.LogError($"Research `{name}` not found");
                    }
                    return new Facility()
                    {
                        Current = i == -1 ? default : new BufferedResearch()
                        {
                            Name = researches[i].Name,
                            ResearchTime = researches[i].ResearchTime,
                            Hash = researches[i].Hash,
                        },
                        CurrentProgress = reader.ReadFloat(),
                    };
                }
            ),
            TypeSerializer.ForComponentSimple<Factory>(
                (writer, v) =>
                {
                    writer.Write(v.Current.Name);
                    writer.Write(v.CurrentProgress);
                    writer.Write(v.TotalProgress);
                },
                (reader) =>
                {
                    FixedString32Bytes name = reader.ReadFixedString32();
                    int i = units.IndexOf(v => v.Name == name);
                    if (i == -1)
                    {
                        Debug.LogError($"Unit not found");
                    }
                    return new Factory()
                    {
                        Current = i == -1 ? default : new BufferedProducingUnit()
                        {
                            Name = units[i].Name,
                            Prefab = units[i].Prefab,
                            ProductionTime = units[i].ProductionTime,
                        },
                        CurrentProgress = reader.ReadFloat(),
                        TotalProgress = reader.ReadFloat(),
                    };
                }
            ),
            TypeSerializer.ForComponent<Extractor>(
                (writer, v) =>
                {
                    writer.Write(v.ExtractProgress);
                },
                (BinaryReader reader, ref Extractor v) =>
                {
                    v.ExtractProgress = reader.ReadFloat();
                }
            ),
            TypeSerializer.ForComponent<Transporter>(
                (writer, v) =>
                {
                    writer.Write(v.LoadProgress);
                    writer.Write(v.CurrentLoad);
                },
                (BinaryReader reader, ref Transporter v) =>
                {
                    v.LoadProgress = reader.ReadFloat();
                    v.CurrentLoad = reader.ReadInt();
                }
            ),
            TypeSerializer.ForComponent<Damageable>(
                (writer, v) =>
                {
                    writer.Write(v.Health);
                },
                (BinaryReader reader, ref Damageable v) =>
                {
                    v.Health = reader.ReadFloat();
                }
            ),
            TypeSerializer.ForComponent<Resource>(
                (writer, v) =>
                {
                    writer.Write(v.Amount);
                },
                (BinaryReader reader, ref Resource v) =>
                {
                    v.Amount = reader.ReadInt();
                }
            ),
            TypeSerializer.ForComponentSimple<BuildingPlaceholder>(
                (writer, v) =>
                {
                    int i = buildings.IndexOf(w => w.Prefab == v.BuildingPrefab);
                    if (i == -1) Debug.LogError($"Building prefab `{v.BuildingPrefab}` not found");
                    writer.Write(i);
                    writer.Write(v.CurrentProgress);
                    writer.Write(v.TotalProgress);
                },
                (reader) =>
                {
                    int i = reader.ReadInt();
                    if (i == -1) Debug.LogError($"Building prefab not found");
                    return new BuildingPlaceholder()
                    {
                        BuildingPrefab = buildings[i].Prefab,
                        CurrentProgress = reader.ReadFloat(),
                        TotalProgress = reader.ReadFloat(),
                    };
                }
            ),
            TypeSerializer.ForComponent<Processor>(
                (writer, v) =>
                {
                    writer.Write(v.SourceFile.Name);
                    writer.Write(v.SourceFile.Source.ConnectionId.Value);
                    writer.WriteUnsafe(v.Registers);
                    writer.WriteUnsafe(v.Memory.Memory);
                    writer.Write(v.Crash);
                    writer.Write(v.Signal);
                    writer.Write(v.SignalNotified);
                    writer.Write(v.IncomingTransmissions, (writer, v) =>
                    {
                        writer.WriteUnsafe(v.Data);
                        writer.WriteUnsafe(v.Metadata);
                    });
                    writer.Write(v.OutgoingTransmissions, (writer, v) =>
                    {
                        writer.WriteUnsafe(v.Data);
                        writer.WriteUnsafe(v.Metadata);
                    });
                    writer.Write(v.CommandQueue, (writer, v) =>
                    {
                        writer.WriteUnsafe(v.Id);
                        writer.WriteBytes(&v.Data, v.DataLength);
                    });
                    writer.Write(v.PendrivePlugRequested);
                    writer.Write(v.PendriveUnplugRequested);
                    writer.Write(v.IsKeyRequested);
                    writer.WriteUnsafe(v.InputKey);
                    writer.Write(v.RadarRequest);
                    writer.Write(v.RadarResponse);
                    writer.Write(v.StdOutBufferCursor);
                    writer.Write(v.StdOutBuffer);
                },
                (BinaryReader reader, ref Processor v) =>
                {
                    v.SourceFile.Name = reader.ReadFixedString128();
                    v.SourceFile.Source.ConnectionId.Value = reader.ReadInt();
                    v.Registers = reader.ReadUnsafe<LanguageCore.Runtime.Registers>();
                    v.Memory.Memory = reader.ReadUnsafe<FixedBytes2048>();
                    v.Crash = reader.ReadInt();
                    v.Signal = (LanguageCore.Runtime.Signal)reader.ReadByte();
                    v.SignalNotified = reader.ReadBool();
                    v.IncomingTransmissions = reader.ReadFixedList128<BufferedUnitTransmission>(reader =>
                    {
                        return new BufferedUnitTransmission()
                        {
                            Data = reader.ReadFixedList32Unsafe<byte>(),
                            Metadata = reader.ReadUnsafe<IncomingUnitTransmissionMetadata>(),
                        };
                    });
                    v.OutgoingTransmissions = reader.ReadFixedList128<BufferedUnitTransmissionOutgoing>(reader =>
                    {
                        return new BufferedUnitTransmissionOutgoing()
                        {
                            Data = reader.ReadFixedList32Unsafe<byte>(),
                            Metadata = reader.ReadUnsafe<OutgoingUnitTransmissionMetadata>(),
                        };
                    });
                    v.CommandQueue = reader.ReadFixedList128<UnitCommandRequest>(reader =>
                    {
                        int id = reader.ReadInt();
                        int dataLength = reader.ReadInt();
                        FixedBytes30 data;
                        reader.ReadBytes(&data, dataLength);
                        return new UnitCommandRequest(id, (ushort)dataLength, data);
                    });
                    v.PendrivePlugRequested = reader.ReadBool();
                    v.PendriveUnplugRequested = reader.ReadBool();
                    v.IsKeyRequested = reader.ReadBool();
                    v.InputKey = reader.ReadFixedList128Unsafe<char>();
                    v.RadarRequest = reader.ReadFloat2();
                    v.RadarResponse = reader.ReadFloat3();
                    v.StdOutBufferCursor = reader.ReadInt();
                    v.StdOutBuffer = reader.ReadFixedString512();
                }
            ),
        };
    }

    static List<PrefabIdSerializer> GetPrefabInstanceIdSerializers(EntityManager entityManager)
    {
        DynamicBuffer<BufferedBuilding> buildings = GetSingletonBuffer<BuildingDatabase, BufferedBuilding>(entityManager, false);
        DynamicBuffer<BufferedUnit> units = GetSingletonBuffer<UnitDatabase, BufferedUnit>(entityManager, false);
        DynamicBuffer<BufferedProjectile> projectiles = GetSingletonBuffer<ProjectileDatabase, BufferedProjectile>(entityManager, false);
        PrefabDatabase prefabs = GetSingleton<PrefabDatabase>(entityManager);
        NativeArray<Research>.ReadOnly researches;
        {
            EntityQuery q = entityManager.CreateEntityQuery(typeof(Research));
            researches = q.ToComponentDataArray<Research>(Allocator.Temp).AsReadOnly();
            q.Dispose();
        }

        return new()
        {
            PrefabIdSerializer.ForComponent<CoreComputer>(
                (writer, v) => { },
                (reader, entityManager) =>
                {
                    return entityManager.Instantiate(prefabs.CoreComputer);
                }
            ),
            PrefabIdSerializer.ForComponent<BuildingPrefabInstance>(
                (writer, v) =>
                {
                    if (v.Index == -1) Debug.LogError($"Invalid prefab instance");
                    writer.Write(v.Index);
                },
                (reader, entityManager) =>
                {
                    int index = reader.ReadInt();
                    if (index == -1) Debug.LogError($"Invalid prefab instance");
                    return entityManager.Instantiate(buildings[index].Prefab);
                }
            ),
            PrefabIdSerializer.ForComponent<UnitPrefabInstance>(
                (writer, v) =>
                {
                    if (v.Index == -1) Debug.LogError($"Invalid prefab instance");
                    writer.Write(v.Index);
                },
                (reader, entityManager) =>
                {
                    int index = reader.ReadInt();
                    if (index == -1) Debug.LogError($"Invalid prefab instance");
                    return entityManager.Instantiate(units[index].Prefab);
                }
            ),
        };
    }

    public static void Save(World serverWorld, string filename)
    {
        EntityManager entityManager = serverWorld.EntityManager;

        DynamicBuffer<BufferedBuilding> buildings = GetSingletonBuffer<BuildingDatabase, BufferedBuilding>(entityManager, false);
        DynamicBuffer<BufferedUnit> units = GetSingletonBuffer<UnitDatabase, BufferedUnit>(entityManager, false);
        DynamicBuffer<BufferedProjectile> projectiles = GetSingletonBuffer<ProjectileDatabase, BufferedProjectile>(entityManager, false);
        DynamicBuffer<BufferedSpawn> spawns = GetSingletonBuffer<Spawns, BufferedSpawn>(entityManager, false);
        PrefabDatabase prefabs = GetSingleton<PrefabDatabase>(entityManager);

        using BinaryWriter writer = new FileBinaryWriter(filename);

        {
            EntityQuery q = entityManager.CreateEntityQuery(typeof(Player));
            NativeArray<Entity> e = q.ToEntityArray(Allocator.Temp);
            writer.Write(e.Length);
            foreach (Entity entity in e)
            {
                Player player = entityManager.GetComponentData<Player>(entity);
                DynamicBuffer<BufferedAcquiredResearch> acquiredResearches = entityManager.GetBuffer<BufferedAcquiredResearch>(entity);

                writer.Write(player.Guid);
                writer.Write(player.ConnectionState);
                writer.Write(player.IsCoreComputerSpawned);
                writer.Write(player.Nickname);
                writer.Write(player.Outcome);
                writer.Write(player.Resources);
                writer.Write(player.Team);

                writer.Write(acquiredResearches, (writer, i) =>
                {
                    writer.Write(i.Name);
                });
            }
            q.Dispose();
        }

        {
            writer.Write(spawns, (writer, v) =>
            {
                writer.Write(v.IsOccupied);
            });
        }

        {
            FileChunkManagerSystem fileChunkManager = FileChunkManagerSystem.GetInstance(serverWorld);
            writer.Write(fileChunkManager.RemoteFiles, (writer, v) =>
            {
                writer.Write(v.Key.Name);
                writer.Write(v.Key.Source.ConnectionId.Value);
                writer.Write(v.Value.Kind);
                writer.Write(v.Value.File.Data);
                writer.Write(v.Value.File.Version);
            });
        }

        {
            CompilerSystemServer compilerSystem = serverWorld.GetExistingSystemManaged<CompilerSystemServer>();
            writer.Write(compilerSystem.CompiledSources, (writer, v) =>
            {
                writer.Write(v.Key.Name);
                writer.Write(v.Key.Source.ConnectionId.Value);
            });
        }

        List<TypeSerializer> types = GetSerializers(entityManager);
        List<PrefabIdSerializer> prefabTypes = GetPrefabInstanceIdSerializers(entityManager);

        NativeList<EntityArchetype> archetypes = new(Allocator.Temp);
        entityManager.GetAllArchetypes(archetypes);

        using NativeArray<ArchetypeChunk> chunks = entityManager.GetAllChunks(Allocator.Temp);

        List<(EntityArchetype Archetype, int PrefabSerializer)> saveableArchetypes = new();
        foreach (EntityArchetype archetype in archetypes)
        {
            if (archetype.Disabled) continue;
            if (archetype.Prefab) continue;

            NativeArray<ComponentType> componentTypes = archetype.GetComponentTypes(Allocator.Temp);

            int prefabIndex = prefabTypes.FindIndex(v => componentTypes.Any(w => v.Is(w)));
            if (prefabIndex == -1) continue;

            if (!chunks.Any(v => v.Archetype == archetype && v.Count > 0)) continue;

            saveableArchetypes.Add((archetype, prefabIndex));
        }

        writer.Write(saveableArchetypes.Count);

        foreach ((EntityArchetype archetype, int prefabIndex) in saveableArchetypes)
        {
            NativeArray<ComponentType> componentTypes = archetype.GetComponentTypes(Allocator.Temp);

            writer.Write(prefabIndex);

            int[] typeIndices = componentTypes.Select(v => types.FindIndex(w => v == w.Type)).Where(v => v != -1).ToArray();
            writer.Write(typeIndices, (w, v) => w.Write(v));

            ArchetypeChunk[] archetypeChunks = chunks.Where(v => v.Archetype == archetype).ToArray();
            int entityCount = archetypeChunks.Sum(v => v.Count);

            writer.Write(entityCount);

            foreach (ArchetypeChunk chunk in archetypeChunks)
            {
                using NativeArray<Entity> entities = chunk.GetNativeArray(entityManager.GetEntityTypeHandle());
                foreach (Entity entity in entities)
                {
                    prefabTypes[prefabIndex].Serializer(writer, entity, entityManager);
                    foreach (int typeIndex in typeIndices)
                    {
                        types[typeIndex].Serializer(writer, entity, entityManager);
                    }
                }
            }
        }
    }

    public static void Load(World serverWorld, EntityCommandBuffer commandBuffer, string filename)
    {
        EntityManager entityManager = serverWorld.EntityManager;

        DynamicBuffer<BufferedBuilding> buildings = GetSingletonBuffer<BuildingDatabase, BufferedBuilding>(entityManager, false);
        DynamicBuffer<BufferedUnit> units = GetSingletonBuffer<UnitDatabase, BufferedUnit>(entityManager, false);
        DynamicBuffer<BufferedProjectile> projectiles = GetSingletonBuffer<ProjectileDatabase, BufferedProjectile>(entityManager, false);
        DynamicBuffer<BufferedSpawn> spawns = GetSingletonBuffer<Spawns, BufferedSpawn>(entityManager, false);
        PrefabDatabase prefabs = GetSingleton<PrefabDatabase>(entityManager);

        using BinaryReader reader = new FileBinaryReader(filename);

        {
            int playerCount = reader.ReadInt();
            for (int i = 0; i < playerCount; i++)
            {
                Entity newPlayer = commandBuffer.Instantiate(prefabs.Player);
                Player player = new()
                {
                    ConnectionId = -1,
                    ConnectionState = PlayerConnectionState.Disconnected,
                    Team = -1,
                    IsCoreComputerSpawned = false,
                    Guid = default,
                    Nickname = default,
                };

                player.Guid = reader.ReadGuid();
                PlayerConnectionState connectionState = (PlayerConnectionState)reader.ReadByte();
                if (connectionState == PlayerConnectionState.Connected) connectionState = PlayerConnectionState.Disconnected;
                player.ConnectionState = connectionState;
                player.IsCoreComputerSpawned = reader.ReadBool();
                player.Nickname = reader.ReadFixedString32();
                player.Outcome = (GameOutcome)reader.ReadByte();
                player.Resources = reader.ReadFloat();
                player.Team = reader.ReadInt();

                commandBuffer.SetComponent(newPlayer, player);

                reader.ReadDynamicBuffer(commandBuffer.SetBuffer<BufferedAcquiredResearch>(newPlayer), reader =>
                {
                    BufferedAcquiredResearch res = default;
                    res.Name = reader.ReadFixedString64();
                    return res;
                });
            }
        }

        {
            reader.ReadDynamicBuffer<BufferedSpawn>(spawns, (BinaryReader reader, ref BufferedSpawn item) =>
            {
                item.IsOccupied = reader.ReadBool();
            });
        }

        {
            FileChunkManagerSystem fileChunkManager = FileChunkManagerSystem.GetInstance(serverWorld);
            foreach (KeyValuePair<FileId, RemoteFile> item in reader.ReadArray((reader) =>
            {
                FileId key = new(reader.ReadFixedString128(), new NetcodeEndPoint(new Unity.NetCode.NetworkId() { Value = reader.ReadInt() }, Entity.Null));
                RemoteFile value = new((FileResponseStatus)reader.ReadInt(), new FileData(reader.ReadArray(v => v.ReadByte()), reader.ReadLong()), key);
                return new KeyValuePair<FileId, RemoteFile>(key, value);
            }))
            {
                fileChunkManager.RemoteFiles.Add(item.Key, item.Value);
            }
        }

        {
            CompilerSystemServer compilerSystem = serverWorld.GetExistingSystemManaged<CompilerSystemServer>();
            foreach (FileId source in reader.ReadArray((reader) =>
            {
                return new FileId(reader.ReadFixedString128(), new NetcodeEndPoint(new Unity.NetCode.NetworkId() { Value = reader.ReadInt() }, Entity.Null));
            }))
            {
                compilerSystem.CompiledSources.Add(source, new(
                    source,
                    default,
                    1,
                    1,
                    CompilationStatus.Secuedued,
                    0,
                    false,
                    default,
                    default,
                    default,
                    new LanguageCore.DiagnosticsCollection()
                ));
            }

            while (compilerSystem.CompiledSources.Any(v => v.Value.Status != CompilationStatus.Done))
            {
                serverWorld.Update();
            }
        }

        List<TypeSerializer> types = GetSerializers(entityManager);
        List<PrefabIdSerializer> prefabTypes = GetPrefabInstanceIdSerializers(entityManager);

        int saveableArchetypesCount = reader.ReadInt();

        for (int i = 0; i < saveableArchetypesCount; i++)
        {
            int prefabIndex = reader.ReadInt();

            int[] typeIndices = reader.ReadArray(static v => v.ReadInt());

            int entityCount = reader.ReadInt();

            for (int j = 0; j < entityCount; j++)
            {
                Entity entity = prefabTypes[prefabIndex].Deserializer(reader, entityManager);
                foreach (int typeIndex in typeIndices)
                {
                    types[typeIndex].Deserializer(reader, entity, entityManager);
                }
            }
        }
    }
}
