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
    public const int CyclesPerTick = 128;

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
        public required NativeList<OwnedData<BufferedUIElement>>.ParallelWriter UIElements;
        public required int* Crash;
        public required Signal* Signal;
        public required Registers* Registers;
        public required RefRO<UnitTeam> Team;

        public void Push(scoped ReadOnlySpan<byte> data)
        {
            Registers->StackPointer += data.Length * BytecodeProcessor.StackDirection;
            ((nint)Memory).Set(Registers->StackPointer, data);

            if (Registers->StackPointer >= global::Processor.UserMemorySize ||
                Registers->StackPointer < global::Processor.HeapSize)
            {
                *Signal = LanguageCore.Runtime.Signal.StackOverflow;
#if !UNITY
            throw new RuntimeException("Stack overflow", GetContext(), default);
#endif
            }
        }

        public unsafe void Push<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
        {
            fixed (void* ptr = data)
            {
                Push(new ReadOnlySpan<byte>(ptr, data.Length * sizeof(T)));
            }
        }

        public Span<byte> Pop(int size)
        {
            if (Registers->StackPointer >= global::Processor.UserMemorySize ||
                Registers->StackPointer < global::Processor.HeapSize)
            {
                *Signal = LanguageCore.Runtime.Signal.StackOverflow;
#if !UNITY
            throw new RuntimeException("Stack overflow", GetContext(), default);
#endif
            }

            Span<byte> data = new Span<byte>(Memory, global::Processor.UserMemorySize).Get(Registers->StackPointer, size);
            Registers->StackPointer -= size * BytecodeProcessor.StackDirection;
            return data;
        }

        public void DoCrash(in FixedString32Bytes message)
        {
            Push(new ReadOnlySpan<char>(message.GetUnsafePtr(), message.Length));
            *Crash = Registers->StackPointer;
            *Signal = LanguageCore.Runtime.Signal.UserCrash;
            return;
        }

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
        int type = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        if (type <= (int)BufferedUIElementType.MIN ||
            type >= (int)BufferedUIElementType.MAX)
        {
            scope->DoCrash("Invalid UI type");
            return;
        }

        int id = 1;
        while (true)
        {
            bool exists = false;

            for (int i = 0; i < scope->UIElements.ListData->Length; i++)
            {
                if ((*scope->UIElements.ListData)[i].Value.Id == id)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists) break;
            id++;
        }

        returnValue.Set(id);

        scope->UIElements.AddNoResize(new(
            scope->Team.ValueRO.Team,
            new BufferedUIElement()
            {
                Type = (BufferedUIElementType)type,
                Id = id,
                Position = new int2(16, 16),
                Size = new int2(128, 128),
                Color = new float3(1f, 1f, 1f),
                Text = new FixedString32Bytes(),
            }
        ));
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_destroy(nint _scope, nint arguments, nint returnValue)
    {
        int id = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        for (int i = 0; i < scope->UIElements.ListData->Length; i++)
        {
            if ((*scope->UIElements.ListData)[i].Value.Id == id)
            {
                (*scope->UIElements.ListData)[i] = default;
                break;
            }
        }
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_set_text(nint _scope, nint arguments, nint returnValue)
    {
        (int id, int stringPtr) = ExternalFunctionGenerator.TakeParameters<int, int>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        scope->GetString(stringPtr, out FixedString32Bytes @string);

        for (int i = 0; i < scope->UIElements.ListData->Length; i++)
        {
            ref var item = ref (*scope->UIElements.ListData).Ptr[i];
            if (item.Value.Id == id)
            {
                item.Value.Text = @string;
            }
        }
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _gui_set_pos(nint _scope, nint arguments, nint returnValue)
    {
        (int id, int2 pos) = ExternalFunctionGenerator.TakeParameters<int, int2>(arguments);
        FunctionScope* scope = (FunctionScope*)_scope;

        for (int i = 0; i < scope->UIElements.ListData->Length; i++)
        {
            ref var item = ref (*scope->UIElements.ListData).Ptr[i];
            if (item.Value.Id == id)
            {
                item.Value.Position = pos;
            }
        }
    }

    public const int ExternalFunctionCount = 26;

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

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_create).Value, 61, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<int>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_destroy).Value, 62, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_set_text).Value, 63, ExternalFunctionGenerator.SizeOf<int, int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_gui_set_pos).Value, 64, ExternalFunctionGenerator.SizeOf<int, int2>(), 0, default);

        Unity.Assertions.Assert.AreEqual(i, ExternalFunctionCount);
    }

    NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    NativeList<OwnedData<BufferedLine>> debugLines;
    NativeList<OwnedData<BufferedWorldLabel>> worldLabels;
    NativeList<OwnedData<BufferedUIElement>> uiElements;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WorldLabelSettings>();
        scopedExternalFunctions = new NativeArray<ExternalFunctionScopedSync>(ExternalFunctionCount, Allocator.Persistent);
        GenerateExternalFunctions((ExternalFunctionScopedSync*)scopedExternalFunctions.GetUnsafePtr());
        debugLines = new(256, Allocator.Persistent);
        worldLabels = new(256, Allocator.Persistent);
        uiElements = new(256, Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (player, lines, labels, _uiElements) in
            SystemAPI.Query<RefRO<Player>, DynamicBuffer<BufferedLine>, DynamicBuffer<BufferedWorldLabel>, DynamicBuffer<BufferedUIElement>>())
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

            _uiElements.Clear();

            for (int i = 0; i < uiElements.Length; i++)
            {
                if (uiElements[i].Owner != player.ValueRO.Team) continue;
                _uiElements.Add(uiElements[i].Value);
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
    public NativeList<OwnedData<BufferedUIElement>>.ParallelWriter uiElements;

    [BurstCompile(CompileSynchronously = true)]
    unsafe void Execute(
        RefRW<Processor> processor,
        RefRO<UnitTeam> team,
        RefRO<LocalToWorld> worldTransform,
        RefRO<LocalTransform> localTransform,
        DynamicBuffer<BufferedInstruction> code,
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

        ExternalFunctionScopedSync* scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[ProcessorSystemServer.ExternalFunctionCount];

        for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
        {
            scopedExternalFunctions[i] = this.scopedExternalFunctions[i];
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
        for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
        { scopedExternalFunctions[i].Scope = (nint)(void*)&scope; }

        ProcessorState processorState = new(
            ProcessorSystemServer.BytecodeInterpreterSettings,
            processor.ValueRW.Registers,
            new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
            new ReadOnlySpan<Instruction>(code.GetUnsafeReadOnlyPtr(), code.Length),
            ReadOnlySpan<IExternalFunction>.Empty,
            scopedExternalFunctions,
            ProcessorSystemServer.ExternalFunctionCount
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
    }
}
