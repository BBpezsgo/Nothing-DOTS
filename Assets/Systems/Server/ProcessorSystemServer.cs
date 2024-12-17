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
using Unity.Profiling;
using Unity.Transforms;

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
        public required NativeList<BufferedLine>.ParallelWriter DebugLines;
        public required int* Crash;
        public required Signal* Signal;
        public required Registers* Registers;

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

        scope->Push<char>("Eh");
        *scope->Crash = scope->Registers->StackPointer;
        *scope->Signal = Signal.UserCrash;
        return;

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
        scope->Processor.ValueRW.OutgoingTransmissions.Add(new BufferedUnitTransmissionOutgoing(scope->WorldTransform.ValueRO.Position, direction, data, cosAngle, angle));
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

        UnitProcessorSystem.MappedMemory* mapped = (UnitProcessorSystem.MappedMemory*)((nint)scope->Memory + Processor.MappedMemoryStart);

        float3 direction;
        direction.x = math.cos(mapped->RadarDirection);
        direction.y = 0f;
        direction.z = math.sin(mapped->RadarDirection);

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

        (float2 position, byte color) = ExternalFunctionGenerator.TakeParameters<float2, byte>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        scope->DebugLines.AddNoResize(new BufferedLine(new float3x2(
            scope->WorldTransform.ValueRO.Position,
            new float3(position.x, 0.5f, position.y)
        ), color, MonoTime.Now + DebugLineDuration));

#if DEBUG_LINES
        Debug.DrawLine(
            scope->WorldTransform.ValueRO.Position,
            new float3(position.x, 0.5f, position.y),
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

        (float2 position, byte color) = ExternalFunctionGenerator.TakeParameters<float2, byte>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.TransformPoint(new float3(position.x, 0f, position.y));

        scope->DebugLines.AddNoResize(new BufferedLine(new float3x2(
            scope->WorldTransform.ValueRO.Position,
            new float3(transformed.x, 0.5f, transformed.z)
        ), color, MonoTime.Now + DebugLineDuration));

#if DEBUG_LINES
        Debug.DrawLine(
            scope->WorldTransform.ValueRO.Position,
            new float3(transformed.x, 0.5f, transformed.z),
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
    static void _toglobal(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();
#endif

        int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (ptr <= 0 || ptr <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;
        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        float2 point = memory.Get<float2>(ptr);
        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.TransformPoint(new float3(point.x, 0f, point.y));
        memory.Set(ptr, new float2(transformed.x, transformed.z));
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
        float2 point = memory.Get<float2>(ptr);
        RefRO<LocalTransform> transform = scope->LocalTransform;
        float3 transformed = transform.ValueRO.InverseTransformPoint(new float3(point.x, 0f, point.y));
        memory.Set(ptr, new float2(transformed.x, transformed.z));
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

    public const int ExternalFunctionCount = 20;

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

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_debug).Value, 41, ExternalFunctionGenerator.SizeOf<float2, byte>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_ldebug).Value, 42, ExternalFunctionGenerator.SizeOf<float2, byte>(), 0, default);

        buffer[i++] = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_dequeue_command).Value, 51, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<int>(), default);

        Unity.Assertions.Assert.AreEqual(i, ExternalFunctionCount);
    }

    NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    NativeList<BufferedLine> debugLines;

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DebugLines>();
        scopedExternalFunctions = new NativeArray<ExternalFunctionScopedSync>(ExternalFunctionCount, Allocator.Persistent);
        GenerateExternalFunctions((ExternalFunctionScopedSync*)scopedExternalFunctions.GetUnsafePtr());
        debugLines = new NativeList<BufferedLine>(256, Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BufferedLine> lines = SystemAPI.GetSingletonBuffer<BufferedLine>();
        for (int i = 0; i < debugLines.Length; i++)
        {
            for (int j = 0; j < lines.Length; j++)
            {
                if (debugLines[i].Value.Equals(lines[j].Value))
                {
                    lines[j] = lines[j] with
                    {
                        DieAt = debugLines[i].DieAt
                    };
                    goto next;
                }
            }
            goto add;
        next:
            continue;
        add:
            lines.Add(debugLines[i]);
        }
        debugLines.Clear();

        new ProcessorJob()
        {
            scopedExternalFunctions = scopedExternalFunctions,
            debugLines = debugLines.AsParallelWriter(),
        }.ScheduleParallel();
    }
}

[BurstCompile(CompileSynchronously = true)]
[WithAll(typeof(Processor), typeof(LocalToWorld), typeof(LocalTransform))]
partial struct ProcessorJob : IJobEntity
{
    [ReadOnly] public NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    public NativeList<BufferedLine>.ParallelWriter debugLines;

    [BurstCompile(CompileSynchronously = true)]
    unsafe void Execute(
        RefRW<Processor> processor,
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

        // Buffer.MemoryCopy(this.scopedExternalFunctions.GetUnsafeReadOnlyPtr(), scopedExternalFunctions, ProcessorSystemServer.ExternalFunctionCount * sizeof(ExternalFunctionScopedSync), ProcessorSystemServer.ExternalFunctionCount * sizeof(ExternalFunctionScopedSync));

        ProcessorSystemServer.FunctionScope transmissionScope = new()
        {
            Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
            Processor = processor,
            WorldTransform = worldTransform,
            LocalTransform = localTransform,
            DebugLines = debugLines,
            Crash = null,
            Registers = null,
            Signal = null,
        };
        for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
        { scopedExternalFunctions[i].Scope = (nint)(void*)&transmissionScope; }

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

        transmissionScope.Crash = &processorState.Crash;
        transmissionScope.Signal = &processorState.Signal;
        transmissionScope.Registers = &processorState.Registers;

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
                            Debug.LogError("Halted");
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
