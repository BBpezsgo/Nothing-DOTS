using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential, Size = 1)]
[GenerateTestsForBurstCompatibility]
struct Memory
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    [GenerateTestsForBurstCompatibility]
    public struct Unmanaged
    {
        [StructLayout(LayoutKind.Sequential, Size = 1)]
        [GenerateTestsForBurstCompatibility]
        public struct Array
        {
            public unsafe static void* Resize(void* oldPointer, long oldCount, long newCount, AllocatorManager.AllocatorHandle allocator, long size, int align)
            {
                int num = math.max(64, align);

                if (allocator.Index >= 64) throw new NotSupportedException();

                void* ptr = default;
                if (newCount > 0)
                {
                    long size2 = newCount * size;
                    CheckByteCountIsReasonable(size2);
                    ptr = UnsafeUtility.MallocTracked(size2, num, allocator.ToAllocator, 0);
                    if (oldCount > 0)
                    {
                        long size3 = math.min(oldCount, newCount) * size;
                        CheckByteCountIsReasonable(size3);
                        UnsafeUtility.MemCpy(ptr, oldPointer, size3);
                    }
                }

                if (oldCount > 0)
                {
                    UnsafeUtility.FreeTracked(oldPointer, allocator.ToAllocator);
                }

                return ptr;
            }

            [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(int) })]
            public unsafe static T* Resize<T>(T* oldPointer, long oldCount, long newCount, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
                => (T*)Resize(oldPointer, oldCount, newCount, allocator, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
        }

        public unsafe static void* Allocate(long size, int align, AllocatorManager.AllocatorHandle allocator)
            => Array.Resize(null, 0L, 1L, allocator, size, align);

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(int) })]
        public unsafe static void Free<T>(T* pointer, AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            if (pointer == null) return;
            Array.Resize(pointer, 1L, 0L, allocator);
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    [Conditional("UNITY_DOTS_DEBUG")]
    public static void CheckByteCountIsReasonable(long size)
    {
        if (size < 0)
        {
            throw new InvalidOperationException($"Attempted to operate on {size} bytes of memory: negative size");
        }

        if (size > 1099511627776L)
        {
            throw new InvalidOperationException($"Attempted to operate on {size} bytes of memory: size too big");
        }
    }
}
[StructLayout(LayoutKind.Explicit)]
[NoAlias]
struct BufferHeader
{
    public enum TrashMode
    {
        TrashOldData,
        RetainOldData
    }

    [FieldOffset(0), NoAlias] public unsafe byte* Pointer;
    [FieldOffset(8)] public int Length;
    [FieldOffset(12)] public int Capacity;

    public unsafe static byte* GetElementPointer(BufferHeader* header)
    {
        if (header->Pointer != null)
        {
            return header->Pointer;
        }

        return (byte*)(header + 1);
    }

    public unsafe static void EnsureCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern, byte memoryInitPattern)
    {
        if (count <= header->Capacity) return;
        int count2 = Math.Max(8, Math.Max(2 * header->Capacity, count));
        SetCapacity(header, count2, typeSize, alignment, trashMode, useMemoryInitPattern, memoryInitPattern, 0);
    }

    public unsafe static void SetCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern, byte memoryInitPattern, int internalCapacity)
    {
        if (count == header->Capacity) return;

        long num = (long)count * (long)typeSize;
        byte* elementPointer = GetElementPointer(header);
        byte* ptr = (byte*)((count <= internalCapacity) ? (header + 1) : Memory.Unmanaged.Allocate(num, alignment, Allocator.Persistent));
        if (elementPointer != ptr)
        {
            if (useMemoryInitPattern)
            {
                if (trashMode == TrashMode.RetainOldData)
                {
                    int num2 = header->Capacity * typeSize;
                    long num3 = num - num2;
                    if (num3 > 0)
                    {
                        UnsafeUtility.MemSet(ptr + num2, memoryInitPattern, num3);
                    }
                }
                else
                {
                    UnsafeUtility.MemSet(ptr, memoryInitPattern, num);
                }
            }

            if (trashMode == TrashMode.RetainOldData)
            {
                long size = Math.Min((long)header->Capacity, (long)count) * typeSize;
                UnsafeUtility.MemCpy(ptr, elementPointer, size);
            }

            if (header->Pointer != null)
            {
                Memory.Unmanaged.Free(header->Pointer, Allocator.Persistent);
            }
        }

        header->Pointer = (ptr == header + 1) ? null : ptr;
        header->Capacity = count;
    }
}

[NativeContainer]
public readonly struct UnsafeDynamicBuffer<T> : IIndexable<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    [NoAlias]
    readonly unsafe BufferHeader* m_Buffer;
    readonly int m_InternalCapacity;
    readonly AtomicSafetyHandle m_Safety0;
    readonly AtomicSafetyHandle m_Safety1;
    readonly int m_SafetyReadOnlyCount;
    readonly int m_SafetyReadWriteCount;
    readonly byte m_IsReadOnly;
    readonly byte m_useMemoryInitPattern;
    readonly byte m_memoryInitPattern;

    public unsafe int Length
    {
        get => m_Buffer->Length;
        set => ResizeUninitialized(value);
    }

    public unsafe int Capacity => m_Buffer->Capacity;

    public bool IsEmpty => !IsCreated || Length == 0;

    public unsafe bool IsCreated => m_Buffer != null;

    public unsafe T this[int index]
    {
        get
        {
            CheckBounds(index);
            return UnsafeUtility.ReadArrayElement<T>(BufferHeader.GetElementPointer(m_Buffer), index);
        }
        set
        {
            CheckBounds(index);
            UnsafeUtility.WriteArrayElement(BufferHeader.GetElementPointer(m_Buffer), index, value);
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    [Conditional("UNITY_DOTS_DEBUG")]
    void CheckBounds(int index)
    {
        if ((uint)index >= (uint)Length)
        {
            throw new IndexOutOfRangeException($"Index {index} is out of range in DynamicBuffer of '{Length}' Length.");
        }
    }

    public unsafe ref T ElementAt(int index)
    {
        CheckBounds(index);
        return ref UnsafeUtility.ArrayElementAsRef<T>(BufferHeader.GetElementPointer(m_Buffer), index);
    }

    public unsafe void EnsureCapacity(int length)
    {
        BufferHeader.EnsureCapacity(m_Buffer, length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), BufferHeader.TrashMode.RetainOldData, m_useMemoryInitPattern == 1, m_memoryInitPattern);
    }

    public unsafe void ResizeUninitialized(int length)
    {
        EnsureCapacity(length);
        m_Buffer->Length = length;
    }

    public int Add(T elem)
    {
        int length = Length;
        ResizeUninitialized(length + 1);
        this[length] = elem;
        return length;
    }

    public unsafe void RemoveAt(int index)
    {
        CheckBounds(index);
        int num = UnsafeUtility.SizeOf<T>();
        byte* elementPointer = BufferHeader.GetElementPointer(m_Buffer);
        UnsafeUtility.MemMove(elementPointer + index * num, elementPointer + (index + 1) * num, (long)num * (long)(Length - 1 - index));
        m_Buffer->Length--;
    }
}
