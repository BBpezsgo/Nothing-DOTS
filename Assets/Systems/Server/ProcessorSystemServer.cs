#define _UNITY_PROFILER
#if UNITY_EDITOR && EDITOR_DEBUG
#define DEBUG_LINES
#endif

using System;
using System.Runtime.CompilerServices;
using AOT;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Profiling;
using Unity.Transforms;
using System.Runtime.InteropServices;

public enum UserUIElementType : byte
{
    MIN,
    Label,
    Image,
    MAX,
}

[BurstCompile]
[StructLayout(LayoutKind.Explicit)]
public struct UserUIElement
{
    [FieldOffset(0)] public bool IsDirty;
    [FieldOffset(1)] public int Id;
    [FieldOffset(5)] public UserUIElementType Type;
    [FieldOffset(6)] public int2 Position;
    [FieldOffset(14)] public int2 Size;
    [FieldOffset(22)] public UserUIElementLabel Label;
    [FieldOffset(22)] public UserUIElementImage Image;
}

[BurstCompile]
[StructLayout(LayoutKind.Sequential)]
public struct UserUIElementLabel
{
    public float3 Color;
    public FixedBytes30 Text;
}

[BurstCompile]
[StructLayout(LayoutKind.Sequential)]
public struct UserUIElementImage
{
    public short Width;
    public short Height;
    public FixedBytes510 Image;
}

struct OwnedData<T>
{
    public readonly int Owner;
    public T Value;

    public OwnedData(int owner, T value)
    {
        Owner = owner;
        Value = value;
    }
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct ProcessorSystemServer : ISystem
{
    public const int CyclesPerTick = 256;

#if UNITY_PROFILER
    static readonly ProfilerMarker _ProcessMarker = new("ProcessorSystemServer.Process");
    static readonly ProfilerMarker _ExternalMarker_stdout = new("ProcessorSystemServer.External.stdout");
    static readonly ProfilerMarker _ExternalMarker_send = new("ProcessorSystemServer.External.send");
    static readonly ProfilerMarker _ExternalMarker_receive = new("ProcessorSystemServer.External.receive");
    static readonly ProfilerMarker _ExternalMarker_radar = new("ProcessorSystemServer.External.radar");
    static readonly ProfilerMarker _ExternalMarker_debug = new("ProcessorSystemServer.External.debug");
    static readonly ProfilerMarker _ExternalMarker_time = new("ProcessorSystemServer.External.time");
    static readonly ProfilerMarker _ExternalMarker_math = new("ProcessorSystemServer.External.math");
    static readonly ProfilerMarker _ExternalMarker_other = new("ProcessorSystemServer.External.other");
#endif

    const float DebugLineDuration = 0.5f;

    public static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };

    [BurstCompile]
    public ref struct FunctionScope
    {
        public required RefRW<Processor> Processor;
        public required void* Memory;
        public required RefRO<LocalToWorld> WorldTransform;
        public required RefRO<LocalTransform> LocalTransform;
        public required NativeList<OwnedData<BufferedLine>>.ParallelWriter DebugLines;
        public required NativeList<OwnedData<BufferedWorldLabel>>.ParallelWriter WorldLabels;
        public required NativeList<OwnedData<UserUIElement>>.ParallelWriter UIElements;
        public required int* Crash;
        public required Signal* Signal;
        public required Registers* Registers;
        public required RefRO<UnitTeam> Team;

        [BurstCompile]
        public void Push(scoped ReadOnlySpan<byte> data)
        {
            Registers->StackPointer += data.Length * ProcessorState.StackDirection;

            if (Registers->StackPointer >= global::Processor.UserMemorySize ||
                Registers->StackPointer < global::Processor.HeapSize)
            {
                *Signal = LanguageCore.Runtime.Signal.StackOverflow;
                return;
            }

            ((nint)Memory).Set(Registers->StackPointer, data);
        }

        [BurstCompile]
        public void DoCrash(in FixedString32Bytes message)
        {
            char* ptr = stackalloc char[message.Length * sizeof(char)];
            Unicode.Utf8ToUtf16(message.GetUnsafePtr(), message.Length, ptr, out int utf16Length, message.Length * sizeof(char));
            Push(new Span<byte>(ptr, utf16Length * sizeof(char)));

            *Crash = Registers->StackPointer;
            *Signal = LanguageCore.Runtime.Signal.UserCrash;
        }

        [BurstCompile]
        public readonly void GetString(int pointer, out FixedString32Bytes @string)
        {
            @string = new();
            for (int i = pointer; i < pointer + 32; i += sizeof(char))
            {
                char c = *(char*)((byte*)Memory + i);
                if (c == '\0') break;
                @string.Append(c);
            }
        }
    }

    static readonly Unity.Mathematics.Random _sharedRandom = Unity.Mathematics.Random.CreateFromIndex(420);

    #region Processor Math Accelerators

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _atan2(nint scope, nint arguments, nint returnValue)
    {
        (float a, float b) = ExternalFunctionGenerator.TakeParameters<float, float>(arguments);
        float r = math.atan2(a, b);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _sin(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.sin(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _cos(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.cos(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _tan(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.tan(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _asin(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.asin(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _acos(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.acos(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _atan(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.atan(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _sqrt(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.sqrt(a);
        r.AsBytes().CopyTo(returnValue);
    }

    #endregion

    #region Processor Unit Functions

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _stdout(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_stdout.Auto();
#endif

        char output = arguments.To<char>();
        if (output == '\r') return;
        ((FunctionScope*)_scope)->Processor.ValueRW.StdOutBuffer.AppendShift(output);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _stdin(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        // using ProfilerMarker.AutoScope marker = _ExternalMarker_stdout.Auto();
#endif

        ((FunctionScope*)_scope)->Processor.ValueRW.IsKeyRequested = true;
        ((FunctionScope*)_scope)->Processor.ValueRW.InputKey = default;
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _send(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_send.Auto();
#endif

        FunctionScope* scope = (FunctionScope*)_scope;

        (int bufferPtr, int length, float directionAngle, float angle) = ExternalFunctionGenerator.TakeParameters<int, int, float, float>(arguments);
        if (length <= 0 || length >= 30) throw new Exception("Passed buffer length must be in range [0,30] inclusive");
        if (bufferPtr == 0) throw new Exception($"Passed buffer pointer is null");
        if (bufferPtr < 0 || bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Passed buffer pointer is invalid");

        float3 direction;
        if (angle != 0f)
        {
            direction.x = math.cos(directionAngle);
            direction.y = 0f;
            direction.z = math.sin(directionAngle);
            direction = scope->LocalTransform.ValueRO.TransformDirection(direction);
        }
        else
        {
            direction = default;
        }
        float cosAngle = math.abs(math.cos(angle));

        FixedList32Bytes<byte> data = new();
        data.AddRange((byte*)((nint)scope->Memory + bufferPtr), length);

        if (scope->Processor.ValueRW.OutgoingTransmissions.Length >= scope->Processor.ValueRW.OutgoingTransmissions.Capacity)
        { scope->Processor.ValueRW.OutgoingTransmissions.RemoveAt(0); }
        scope->Processor.ValueRW.OutgoingTransmissions.Add(new()
        {
            Source = scope->WorldTransform.ValueRO.Position,
            Direction = direction,
            Data = data,
            CosAngle = cosAngle,
            Angle = angle,
        });
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _receive(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_receive.Auto();
#endif

        returnValue.Set(0);

        (int bufferPtr, int length, int directionPtr) = ExternalFunctionGenerator.TakeParameters<int, int, int>(arguments);
        if (bufferPtr == 0 || length <= 0) return;
        if (bufferPtr < 0 || bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Passed buffer pointer is invalid");

        FunctionScope* scope = (FunctionScope*)_scope;

        ref FixedList128Bytes<BufferedUnitTransmission> received = ref scope->Processor.ValueRW.IncomingTransmissions; // scope->EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
        if (received.Length == 0) return;

        BufferedUnitTransmission first = received[0];

        int copyLength = math.min(first.Data.Length, length);

        Buffer.MemoryCopy(((byte*)&first.Data) + 2, (byte*)scope->Memory + bufferPtr, copyLength, copyLength);

        if (directionPtr > 0)
        {
            float3 transformed = scope->LocalTransform.ValueRO.InverseTransformPoint(first.Source);
            transformed = math.normalize(transformed);
            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            memory.Set(directionPtr, math.atan2(transformed.z, transformed.x));
        }

        if (copyLength >= first.Data.Length)
        {
            received.RemoveAt(0);
        }
        else
        {
            first.Data.RemoveRange(0, copyLength);
            received[0] = first;
        }

        returnValue.Set(copyLength);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _dequeue_command(nint _scope, nint arguments, nint returnValue)
    {
        returnValue.Set(0);

        int dataPtr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (dataPtr == 0) return;
        if (dataPtr < 0 || dataPtr >= Processor.UserMemorySize) throw new Exception($"Passed data pointer is invalid");

        FunctionScope* scope = (FunctionScope*)_scope;

        ref FixedList128Bytes<UnitCommandRequest> queue = ref scope->Processor.ValueRW.CommandQueue; // scope->EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
        if (queue.Length == 0) return;

        UnitCommandRequest first = queue[0];
        queue.RemoveAt(0);

        Buffer.MemoryCopy(&first.Data, (byte*)scope->Memory + dataPtr, first.DataLength, first.DataLength);

        returnValue.Set(first.Id);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _radar(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_radar.Auto();
#endif

        FunctionScope* scope = (FunctionScope*)_scope;

        MappedMemory* mapped = (MappedMemory*)((nint)scope->Memory + Processor.MappedMemoryStart);

        float3 direction;
        direction.x = math.cos(mapped->Radar.RadarDirection);
        direction.y = 0f;
        direction.z = math.sin(mapped->Radar.RadarDirection);

        scope->Processor.ValueRW.RadarResponse = 0f;
        scope->Processor.ValueRW.RadarRequest = direction;
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _debug(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float3 position, byte color) = ExternalFunctionGenerator.TakeParameters<float3, byte>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        if (scope->DebugLines.ListData->Length + 1 < scope->DebugLines.ListData->Capacity) scope->DebugLines.AddNoResize(new(
            scope->Team.ValueRO.Team,
            new BufferedLine(new float3x2(
                scope->WorldTransform.ValueRO.Position,
                position
            ), color, MonoTime.Now + DebugLineDuration)
        ));

#if DEBUG_LINES
        Debug.DrawLine(
            scope->WorldTransform.ValueRO.Position,
            position,
            new Color(
                (color & 0b100) != 0 ? 1f : 0f,
                (color & 0b010) != 0 ? 1f : 0f,
                (color & 0b001) != 0 ? 1f : 0f
            ),
            DebugLineDuration);
#endif
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _ldebug(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float3 position, byte color) = ExternalFunctionGenerator.TakeParameters<float3, byte>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.TransformPoint(position);

        if (scope->DebugLines.ListData->Length + 1 < scope->DebugLines.ListData->Capacity) scope->DebugLines.AddNoResize(new(
            scope->Team.ValueRO.Team,
            new BufferedLine(new float3x2(
                scope->WorldTransform.ValueRO.Position,
                transformed
            ), color, MonoTime.Now + DebugLineDuration)
        ));

#if DEBUG_LINES
        Debug.DrawLine(
            scope->WorldTransform.ValueRO.Position,
            transformed,
            new Color(
                (color & 0b100) != 0 ? 1f : 0f,
                (color & 0b010) != 0 ? 1f : 0f,
                (color & 0b001) != 0 ? 1f : 0f
            ),
            DebugLineDuration);
#endif
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _debug_label(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float3 position, int textPtr) = ExternalFunctionGenerator.TakeParameters<float3, int>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        FixedString32Bytes text = new();
        for (int i = textPtr; i < textPtr + 32; i += sizeof(char))
        {
            char c = *(char*)((byte*)scope->Memory + i);
            if (c == '\0') break;
            text.Append(c);
        }

        if (scope->WorldLabels.ListData->Length + 1 < scope->WorldLabels.ListData->Capacity) scope->WorldLabels.AddNoResize(new(
            scope->Team.ValueRO.Team,
            new BufferedWorldLabel(position, 0b111, text, MonoTime.Now + DebugLineDuration)
        ));
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _ldebug_label(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float3 position, int textPtr) = ExternalFunctionGenerator.TakeParameters<float3, int>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        FixedString32Bytes text = new();
        for (int i = textPtr; i < textPtr + 32; i += sizeof(char))
        {
            char c = *(char*)((byte*)scope->Memory + i);
            if (c == '\0') break;
            text.Append(c);
        }

        float3 transformed = scope->LocalTransform.ValueRO.TransformPoint(position);

        if (scope->WorldLabels.ListData->Length + 1 < scope->WorldLabels.ListData->Capacity) scope->WorldLabels.AddNoResize(new(
            scope->Team.ValueRO.Team,
            new BufferedWorldLabel(transformed, 0b111, text, MonoTime.Now + DebugLineDuration)
        ));
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _toglobal(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();
#endif

        int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (ptr <= 0 || ptr <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;
        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        float3 point = memory.Get<float3>(ptr);
        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.TransformPoint(point);
        memory.Set(ptr, transformed);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _tolocal(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();
#endif

        int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (ptr <= 0 || ptr <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;
        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        float3 point = memory.Get<float3>(ptr);
        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.InverseTransformPoint(point);
        memory.Set(ptr, transformed);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _time(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_time.Auto();
#endif

        FunctionScope* scope = (FunctionScope*)_scope;
        returnValue.Set(MonoTime.Now);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _random(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();
#endif

        returnValue.Set(_sharedRandom.NextInt());
    }

    #endregion

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_create(nint _scope, nint arguments, nint returnValue)
    {
        int _ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        UserUIElement* ptr = (UserUIElement*)((nint)scope->Memory + _ptr);

        int id = 1;
        while (true)
        {
            bool exists = false;

            for (int i = 0; i < scope->UIElements.ListData->Length; i++)
            {
                if ((*scope->UIElements.ListData)[i].Value.Id != id) continue;
                if ((*scope->UIElements.ListData)[i].Owner != scope->Team.ValueRO.Team) continue;
                exists = true;
                break;
            }

            if (!exists) break;
            id++;
        }

        switch (ptr->Type)
        {
            case UserUIElementType.Label:
                char* text = (char*)&ptr->Label.Text;
                scope->UIElements.AddNoResize(new(
                    scope->Team.ValueRO.Team,
                    *ptr = new UserUIElement()
                    {
                        IsDirty = true,
                        Type = UserUIElementType.Label,
                        Id = id,
                        Position = ptr->Position,
                        Size = ptr->Size,
                        Label = new UserUIElementLabel()
                        {
                            Color = ptr->Label.Color,
                            Text = ptr->Label.Text,
                        },
                    }
                ));
                break;
            case UserUIElementType.Image:
                scope->UIElements.AddNoResize(new(
                    scope->Team.ValueRO.Team,
                    *ptr = new UserUIElement()
                    {
                        IsDirty = true,
                        Type = UserUIElementType.Image,
                        Id = id,
                        Position = ptr->Position,
                        Size = ptr->Size,
                        Image = new UserUIElementImage()
                        {
                            Width = ptr->Image.Width,
                            Height = ptr->Image.Height,
                            Image = ptr->Image.Image,
                        },
                    }
                ));
                break;
            case UserUIElementType.MIN:
            case UserUIElementType.MAX:
            default:
                break;
        }
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_destroy(nint _scope, nint arguments, nint returnValue)
    {
        int id = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        for (int i = 0; i < scope->UIElements.ListData->Length; i++)
        {
            if ((*scope->UIElements.ListData)[i].Value.Id != id) continue;
            if ((*scope->UIElements.ListData)[i].Owner != scope->Team.ValueRO.Team) continue;
            (*scope->UIElements.ListData)[i] = default;
            break;
        }
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_update(nint _scope, nint arguments, nint returnValue)
    {
        int _ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        UserUIElement* ptr = (UserUIElement*)((nint)scope->Memory + _ptr);

        for (int i = 0; i < scope->UIElements.ListData->Length; i++)
        {
            ref var uiElement = ref (*scope->UIElements.ListData).Ptr[i];
            if (uiElement.Value.Id != ptr->Id) continue;
            if (uiElement.Owner != scope->Team.ValueRO.Team) continue;
            switch (ptr->Type)
            {
                case UserUIElementType.Label:
                    char* text = (char*)&ptr->Label.Text;
                    uiElement = new OwnedData<UserUIElement>(
                        scope->Team.ValueRO.Team,
                        *ptr = new UserUIElement()
                        {
                            IsDirty = true,
                            Type = UserUIElementType.Label,
                            Id = ptr->Id,
                            Position = ptr->Position,
                            Size = ptr->Size,
                            Label = ptr->Label,
                        }
                    );
                    break;
                case UserUIElementType.Image:
                    uiElement = new OwnedData<UserUIElement>(
                        scope->Team.ValueRO.Team,
                        *ptr = new UserUIElement()
                        {
                            IsDirty = true,
                            Type = UserUIElementType.Image,
                            Id = ptr->Id,
                            Position = ptr->Position,
                            Size = ptr->Size,
                            Image = ptr->Image,
                        }
                    );
                    break;
            }
            break;
        }
    }

    public const int ExternalFunctionCount = 25;

    [BurstCompile]
    public static void GenerateExternalFunctions(ExternalFunctionScopedSync* buffer)
    {
        int i = 0;

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_stdout).Value, 01, ExternalFunctionGenerator.SizeOf<char>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_stdin).Value, 02, 0, ExternalFunctionGenerator.SizeOf<char>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_sqrt).Value, 11, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_atan2).Value, 12, ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_sin).Value, 13, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_cos).Value, 14, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_tan).Value, 15, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_asin).Value, 16, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_acos).Value, 17, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_atan).Value, 18, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_send).Value, 21, ExternalFunctionGenerator.SizeOf<int, int, float, float>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_receive).Value, 22, ExternalFunctionGenerator.SizeOf<int, int, int>(), ExternalFunctionGenerator.SizeOf<int>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_radar).Value, 23, 0, 0, default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_toglobal).Value, 31, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_tolocal).Value, 32, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_time).Value, 33, 0, ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_random).Value, 34, 0, ExternalFunctionGenerator.SizeOf<int>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_debug).Value, 41, ExternalFunctionGenerator.SizeOf<float3, byte>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_ldebug).Value, 42, ExternalFunctionGenerator.SizeOf<float3, byte>(), 0, default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_debug_label).Value, 43, ExternalFunctionGenerator.SizeOf<float3, int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_ldebug_label).Value, 44, ExternalFunctionGenerator.SizeOf<float3, int>(), 0, default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_dequeue_command).Value, 51, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<int>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_create).Value, 61, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_destroy).Value, 62, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_update).Value, 63, ExternalFunctionGenerator.SizeOf<int>(), 0, default);

        Unity.Assertions.Assert.AreEqual(i, ExternalFunctionCount);
    }

    NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    NativeList<OwnedData<BufferedLine>> debugLines;
    NativeList<OwnedData<BufferedWorldLabel>> worldLabels;
    NativeList<OwnedData<UserUIElement>> uiElements;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WorldLabelSettings>();
        scopedExternalFunctions = new NativeArray<ExternalFunctionScopedSync>(ExternalFunctionCount, Allocator.Persistent);
        GenerateExternalFunctions((ExternalFunctionScopedSync*)scopedExternalFunctions.GetUnsafePtr());
        debugLines = new(256, Allocator.Persistent);
        worldLabels = new(256, Allocator.Persistent);
        uiElements = new(256, Allocator.Persistent);

        // SystemAPI.GetSingleton<RpcCollection>()
        //     .RegisterRpc(ComponentType.ReadWrite<UIElementUpdateRpc>(), default(UIElementUpdateRpc).CompileExecute());
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (player, lines, labels) in
            SystemAPI.Query<RefRO<Player>, DynamicBuffer<BufferedLine>, DynamicBuffer<BufferedWorldLabel>>())
        {
            for (int i = 0; i < debugLines.Length; i++)
            {
                if (debugLines[i].Owner != player.ValueRO.Team) continue;

                for (int j = 0; j < lines.Length; j++)
                {
                    if (debugLines[i].Value.Value.Equals(lines[j].Value))
                    {
                        lines.Set(j, debugLines[i].Value);
                        goto next;
                    }
                }
                lines.Add(debugLines[i].Value);
            next:;
            }

            for (int i = 0; i < worldLabels.Length; i++)
            {
                if (worldLabels[i].Owner != player.ValueRO.Team) continue;

                for (int j = 0; j < labels.Length; j++)
                {
                    if (worldLabels[i].Value.Position.Equals(labels[j].Position))
                    {
                        labels.Set(j, worldLabels[i].Value);
                        goto next;
                    }
                }
                labels.Add(worldLabels[i].Value);
            next:;
            }

            for (int i = 0; i < uiElements.Length; i++)
            {
                if (uiElements[i].Owner != player.ValueRO.Team) continue;
                if (!uiElements[i].Value.IsDirty && uiElements[i].Value.Id != 0) continue;

                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                Entity connection = Entity.Null;
                foreach (var (_connection, _connectionEntity) in
                    SystemAPI.Query<RefRO<NetworkId>>()
                    .WithEntityAccess())
                {
                    if (_connection.ValueRO.Value != player.ValueRO.ConnectionId) continue;
                    connection = _connectionEntity;
                    break;
                }

                if (connection == Entity.Null) continue;

                if (uiElements[i].Value.Id == 0)
                {
                    // Debug.Log(string.Format("{0} destroyed", uiElements[i]));

                    Entity rpc = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<SendRpcCommandRequest>(rpc, new()
                    {
                        TargetConnection = connection,
                    });
                    commandBuffer.AddComponent<UIElementDestroyRpc>(rpc, new()
                    {
                        Id = uiElements[i].Value.Id,
                    });
                    uiElements.RemoveAt(i--);
                }
                else
                {
                    // Debug.Log(string.Format("{0} updated, {1}", uiElements[i], uiElements[i].Value.Label.Text.AsString()));

                    Entity rpc = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<SendRpcCommandRequest>(rpc, new()
                    {
                        TargetConnection = connection,
                    });
                    commandBuffer.AddComponent<UIElementUpdateRpc>(rpc, new()
                    {
                        UIElement = uiElements[i].Value,
                    });
                    uiElements.AsArray().AsSpan()[i].Value.IsDirty = false;
                }
            }
        }

        debugLines.Clear();
        worldLabels.Clear();

        new ProcessorJob()
        {
            scopedExternalFunctions = scopedExternalFunctions,
            debugLines = debugLines.AsParallelWriter(),
            worldLabels = worldLabels.AsParallelWriter(),
            uiElements = uiElements.AsParallelWriter(),
        }.ScheduleParallel();
    }
}

[BurstCompile(CompileSynchronously = true)]
[WithAll(typeof(Processor), typeof(UnitTeam), typeof(LocalToWorld), typeof(LocalTransform))]
partial struct ProcessorJob : IJobEntity
{
    [ReadOnly] public NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    public NativeList<OwnedData<BufferedLine>>.ParallelWriter debugLines;
    public NativeList<OwnedData<BufferedWorldLabel>>.ParallelWriter worldLabels;
    public NativeList<OwnedData<UserUIElement>>.ParallelWriter uiElements;

    [BurstCompile(CompileSynchronously = true)]
    unsafe void Execute(
        RefRW<Processor> processor,
        RefRO<UnitTeam> team,
        RefRO<LocalToWorld> worldTransform,
        RefRO<LocalTransform> localTransform,
        DynamicBuffer<BufferedInstruction> code,
        DynamicBuffer<BufferedGeneratedFunction> generatedFunctions,
        Entity entity)
    {
        if (processor.ValueRO.SourceFile == default)
        {
            processor.ValueRW.StatusLED.Status = 0;
            return;
        }

        if (code.IsEmpty)
        {
            processor.ValueRW.StatusLED.Status = 0;
            return;
        }

        ProcessorSystemServer.FunctionScope scope = new()
        {
            Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
            Processor = processor,
            WorldTransform = worldTransform,
            LocalTransform = localTransform,
            DebugLines = debugLines,
            WorldLabels = worldLabels,
            UIElements = uiElements,
            Team = team,
            Crash = null,
            Registers = null,
            Signal = null,
        };

        NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions = new(ProcessorSystemServer.ExternalFunctionCount + generatedFunctions.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
        {
            scopedExternalFunctions[i] = this.scopedExternalFunctions[i];
            ((ExternalFunctionScopedSync*)scopedExternalFunctions.GetUnsafePtr())[i].Scope = (nint)(void*)&scope;
        }

        for (int i = ProcessorSystemServer.ExternalFunctionCount; i < scopedExternalFunctions.Length; i++)
        {
            scopedExternalFunctions[i] = generatedFunctions[i - ProcessorSystemServer.ExternalFunctionCount].V;
        }

        ProcessorState processorState = new(
            ProcessorSystemServer.BytecodeInterpreterSettings,
            processor.ValueRW.Registers,
            new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
            new ReadOnlySpan<Instruction>(code.GetUnsafeReadOnlyPtr(), code.Length),
            scopedExternalFunctions
        )
        {
            Signal = processor.ValueRO.Signal,
            Crash = processor.ValueRO.Crash,
        };

        scope.Crash = &processorState.Crash;
        scope.Signal = &processorState.Signal;
        scope.Registers = &processorState.Registers;

        for (int i = 0; i < ProcessorSystemServer.CyclesPerTick; i++)
        {
            if (processorState.Signal != Signal.None)
            {
                if (!processor.ValueRO.SignalNotified)
                {
                    processor.ValueRW.SignalNotified = true;
                    switch (processorState.Signal)
                    {
                        case Signal.UserCrash:
                            Debug.LogError("Crashed");
                            break;
                        case Signal.StackOverflow:
                            Debug.LogError("Stack Overflow");
                            break;
                        case Signal.Halt:
                            // Debug.LogError("Halted");
                            break;
                        case Signal.UndefinedExternalFunction:
                            Debug.LogError("Undefined External Function");
                            break;
                        case Signal.PointerOutOfRange:
                            Debug.LogError("Pointer out of Range");
                            break;
                    }
                }
                break;
            }
            processor.ValueRW.SignalNotified = false;

            if (processor.ValueRO.IsKeyRequested)
            {
                if (processor.ValueRO.InputKey == default) break;
                char key = processor.ValueRW.InputKey;
                processor.ValueRW.InputKey = default;
                processor.ValueRW.IsKeyRequested = false;
                processorState.Pop(2);
                processorState.Push(key.AsBytes());
            }

            processorState.Process();
        }

        processor.ValueRW.Registers = processorState.Registers;
        processor.ValueRW.Signal = processorState.Signal;
        processor.ValueRW.Crash = processorState.Crash;
        processor.ValueRW.StatusLED.Status = processorState.Signal == Signal.None ? 1 : 2;

        scopedExternalFunctions.Dispose();
    }
}
