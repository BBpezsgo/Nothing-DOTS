#define _UNITY_PROFILER
#define _DEBUG_LINES

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
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(UnitProcessorSystem))]
[BurstCompile]
unsafe partial struct ProcessorSystemServer : ISystem
{
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
        public required Entity SourceEntity;
        public required RefRO<LocalToWorld> WorldTransform;
        public required RefRO<LocalTransform> LocalTransform;
        public required CollisionWorld CollisionWorld;
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
    static void _send(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_send.Auto();
#endif

        (int bufferPtr, int length, float directionAngle, float angle) = ExternalFunctionGenerator.TakeParameters<int, int, float, float>(arguments);
        if (bufferPtr <= 0 || length <= 0) throw new Exception($"Bruh");
        if (length >= 30) throw new Exception($"Can't");
        if (bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Bruh");

        FunctionScope* scope = (FunctionScope*)_scope;

        float3 direction;
        if (angle != 0f)
        {
            direction.x = math.sin(directionAngle);
            direction.y = 0f;
            direction.z = math.cos(directionAngle);
        }
        else
        {
            direction = default;
        }
        float cosAngle = math.abs(math.cos(angle / 2f));

        scope->Processor.ValueRW.NetworkSendLED.Blink();

        FixedList32Bytes<byte> data = new();
        data.AddRange((byte*)((nint)scope->Memory + bufferPtr), length);

        if (scope->Processor.ValueRW.OutgoingTransmissions.Length >= scope->Processor.ValueRW.OutgoingTransmissions.Capacity)
        { scope->Processor.ValueRW.OutgoingTransmissions.RemoveAt(0); }
        scope->Processor.ValueRW.OutgoingTransmissions.Add(new BufferedUnitTransmissionOutgoing(scope->WorldTransform.ValueRO.Position, direction, data, cosAngle));

        /*

        NativeHashMap<uint, NativeList<QuadrantEntity>> map = QuadrantSystem.GetMap(scope->World);
        int2 grid = QuadrantSystem.ToGrid(scope->SourcePosition);

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (!map.TryGetValue(QuadrantSystem.GetKey(grid + new int2(x, z)), out NativeList<QuadrantEntity> cell)) continue;
                for (int i = 0; i < cell.Length; i++)
                {
                    if (cell[i].Entity == scope->SourceEntity) continue;

                    float3 entityLocalPosition = cell[i].Position;
                    entityLocalPosition.x = cell[i].Position.x - scope->SourcePosition.x;
                    entityLocalPosition.y = 0f;
                    entityLocalPosition.z = cell[i].Position.z - scope->SourcePosition.z;

                    float entityDistanceSq = math.lengthsq(entityLocalPosition);
                    if (entityDistanceSq > (Unit.TransmissionRadius * Unit.TransmissionRadius)) continue;

                    if (angle != 0f)
                    {
                        float dot = math.abs(math.dot(direction, entityLocalPosition / math.sqrt(entityDistanceSq)));
                        if (dot < cosAngle) continue;
                    }

                    var other = scope->ProcessorComponentQ.GetRefRWOptional(cell[i].Entity);
                    if (!other.IsValid) continue;

                    ref var transmissions = ref other.ValueRW.IncomingTransmissions;

                    if (transmissions.Length >= transmissions.Capacity) transmissions.RemoveAt(0);
                    transmissions.Add(new BufferedUnitTransmission(scope->SourcePosition, data));
                }
            }
        }

        */

        /*

        using NativeArray<Entity> entities = scope->ProcessorEntitiesQ.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (entities[i] == scope->SourceEntity) continue;

            if (angle != 0f)
            {
                float3 entityDirection = scope->EntityManager.GetComponentData<LocalToWorld>(entities[i]).Position;
                entityDirection.x -= scope->SourcePosition.x;
                entityDirection.y = 0f;
                entityDirection.z -= scope->SourcePosition.z;
                entityDirection = math.normalize(entityDirection);
                float dot = math.abs(math.dot(direction, entityDirection));
                if (dot < cosAngle) continue;
            }

            DynamicBuffer<BufferedUnitTransmission> transmissions = scope->EntityManager.GetBuffer<BufferedUnitTransmission>(entities[i]);
            if (transmissions.Length > 128) transmissions.RemoveAt(0);
            transmissions.Add(new BufferedUnitTransmission(scope->SourcePosition, data));
        }

        */
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _receive(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_receive.Auto();
#endif

        returnValue.Set(0);

        (int bufferPtr, int length, int directionPtr, int strengthPtr) = ExternalFunctionGenerator.TakeParameters<int, int, int, int>(arguments);
        if (bufferPtr <= 0 || length <= 0) return;
        if (bufferPtr + length >= Processor.UserMemorySize) throw new Exception($"Bruh");

        FunctionScope* scope = (FunctionScope*)_scope;

        ref FixedList128Bytes<BufferedUnitTransmission> received = ref scope->Processor.ValueRW.IncomingTransmissions; // scope->EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
        if (received.Length == 0) return;

        BufferedUnitTransmission first = received[0];

        int copyLength = System.Math.Min(first.Data.Length, length);

        Buffer.MemoryCopy(((byte*)&first.Data) + 2, (byte*)scope->Memory + bufferPtr, copyLength, copyLength);

        // for (int i = 0; i < copyLength; i++)
        // {
        //     *((byte*)scope->Memory + bufferPtr + i) = first->Data[i];
        // }

        scope->Processor.ValueRW.NetworkReceiveLED.Blink();

        if (directionPtr > 0)
        {
            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            RefRO<LocalTransform> transform = scope->LocalTransform;
            float3 transformed = transform.ValueRO.InverseTransformPoint(first.Source);
            memory.Set(directionPtr, math.atan2(transformed.x, transformed.z));
        }

        if (strengthPtr > 0)
        {
            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            memory.Set(strengthPtr, (byte)255);
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
    static void _radar(nint _scope, nint arguments, nint returnValue)
    {
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_radar.Auto();
#endif

        returnValue.Set(0f);

        FunctionScope* scope = (FunctionScope*)_scope;

        UnitProcessorSystem.MappedMemory* mapped = (UnitProcessorSystem.MappedMemory*)((nint)scope->Memory + Processor.MappedMemoryStart);

        float3 direction;
        direction.x = math.sin(mapped->RadarDirection);
        direction.y = 0f;
        direction.z = math.cos(mapped->RadarDirection);

        // LocalToWorld radar = scope->EntityManager.GetComponentData<LocalToWorld>(scope->EntityManager.GetComponentData<Unit>(scope->SourceEntity).Radar);

        RaycastInput input = new()
        {
            Start = scope->WorldTransform.ValueRO.Position + (direction * 1f),
            End = scope->WorldTransform.ValueRO.Position + (direction * (Unit.RadarRadius - 1f)),
            Filter = new CollisionFilter()
            {
                BelongsTo = Layers.All,
                CollidesWith = Layers.All,
                GroupIndex = 0,
            },
        };

        if (!scope->CollisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        { return; }

        scope->Processor.ValueRW.RadarLED.Blink();

        float distance = math.distance(hit.Position, input.Start) + 1f;

        if (distance >= Unit.RadarRadius) return;

        returnValue.Set(distance);
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _debug(nint _scope, nint arguments, nint returnValue)
    {
#if DEBUG_LINES
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float2 position, int color) = ExternalFunctionGenerator.TakeParameters<float2, int>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        Debug.DrawLine(
            scope->SourcePosition,
            new Vector3(position.x, 0.5f, position.y),
            new Color(
                (color & 0xFF0000) >> 16,
                (color & 0x00FF00) >> 8,
                (color & 0x0000FF) >> 0
            ),
            1f);
#endif
    }

    [BurstCompile]
    [MonoPInvokeCallback(typeof(ExternalFunctionUnity))]
    static void _ldebug(nint _scope, nint arguments, nint returnValue)
    {
#if DEBUG_LINES
#if UNITY_PROFILER
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();
#endif

        (float2 position, int color) = ExternalFunctionGenerator.TakeParameters<float2, int>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        LocalTransform transform = scope->LocalTransform;
        float3 transformed = transform.TransformPoint(new Vector3(position.x, 0f, position.y));

        Debug.DrawLine(
            scope->SourcePosition,
            new Vector3(transformed.x, 0.5f, transformed.z),
            new Color(
                (color & 0xFF0000) >> 16,
                (color & 0x00FF00) >> 8,
                (color & 0x0000FF) >> 0
            ),
            1f);
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

    public const int ExternalFunctionCount = 18;

    [BurstCompile]
    public static void GenerateExternalFunctions(ExternalFunctionScopedSync* buffer)
    {
        int i = 0;

        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_stdout).Value, 01, ExternalFunctionGenerator.SizeOf<char>(), 0, default);

        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_sqrt).Value, 11, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_atan2).Value, 12, ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_sin).Value, 13, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_cos).Value, 14, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_tan).Value, 15, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_asin).Value, 16, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_acos).Value, 17, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_atan).Value, 18, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_send).Value, 21, ExternalFunctionGenerator.SizeOf<int, int, float, float>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_receive).Value, 22, ExternalFunctionGenerator.SizeOf<int, int, int, int>(), ExternalFunctionGenerator.SizeOf<int>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_radar).Value, 23, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<float>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_toglobal).Value, 31, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_tolocal).Value, 32, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_time).Value, 33, 0, ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_random).Value, 34, 0, ExternalFunctionGenerator.SizeOf<int>(), default);

        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_debug).Value, 41, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default);
        buffer[i++] = new((delegate* unmanaged[Cdecl] <nint, nint, nint, void>)BurstCompiler.CompileFunctionPointer<ExternalFunctionUnity>(_ldebug).Value, 42, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default);

        Unity.Assertions.Assert.AreEqual(i, ExternalFunctionCount);
    }

    NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;

    void ISystem.OnCreate(ref SystemState state)
    {
        scopedExternalFunctions = new NativeArray<ExternalFunctionScopedSync>(ExternalFunctionCount, Allocator.Persistent);
        GenerateExternalFunctions((ExternalFunctionScopedSync*)scopedExternalFunctions.GetUnsafePtr());
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        new ProcessorJob()
        {
            collisionWorld = collisionWorld,
            scopedExternalFunctions = scopedExternalFunctions,
        }.ScheduleParallel();

        /*
        foreach (var (processor, transform, code, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalToWorld>, DynamicBuffer<BufferedInstruction>>()
            .WithEntityAccess())
        {
            if (processor.ValueRO.SourceFile == default)
            {
                processor.ValueRW.StatusLED.Status = 0;
                continue;
            }

            if (code.IsEmpty)
            {
                processor.ValueRW.StatusLED.Status = 0;
                continue;
            }

            FunctionScope transmissionScope = new()
            {
                Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
                EntityManager = state.EntityManager,
                World = state.WorldUnmanaged,
                Processor = processor,
                SourceEntity = entity,
                SourcePosition = transform.ValueRO.Position,
                ProcessorEntitiesQ = processorEntitiesQ,
                ProcessorComponentQ = processorComponentQ,
                CollisionWorld = collisionWorld,
            };
            for (int i = 0; i < ExternalFunctionCount; i++)
            { scopedExternalFunctions[i].Scope = (nint)(&transmissionScope); }

            ProcessorState processorState = new(
                BytecodeInterpreterSettings,
                processor.ValueRW.Registers,
                new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
                new ReadOnlySpan<Instruction>(code.GetUnsafeReadOnlyPtr(), code.Length),
                ReadOnlySpan<IExternalFunction>.Empty,
                scopedExternalFunctions,
                ExternalFunctionCount
            );

#if UNITY_PROFILER
            using (ProfilerMarker.AutoScope marker = _ProcessMarker.Auto())
#endif
            {
                for (int i = 0; i < 128; i++)
                {
                    if (processorState.Registers.CodePointer == processorState.Code.Length) break;
                    processorState.Process();
                    if (processorState.Signal != Signal.None)
                    {
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
                        processorState.Registers.CodePointer = processorState.Code.Length;
                        break;
                    }
                }
            }

            processor.ValueRW.Registers = processorState.Registers;
            processor.ValueRW.StatusLED.Status = 1;
        }
        */
    }
}

[BurstCompile(CompileSynchronously = true)]
[WithAll(typeof(Processor), typeof(LocalToWorld), typeof(LocalTransform))]
partial struct ProcessorJob : IJobEntity
{
    [ReadOnly] public NativeArray<ExternalFunctionScopedSync> scopedExternalFunctions;
    [ReadOnly] public CollisionWorld collisionWorld;

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
            SourceEntity = entity,
            WorldTransform = worldTransform,
            LocalTransform = localTransform,
            CollisionWorld = collisionWorld,
        };
        for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
        { scopedExternalFunctions[i].Scope = (void*)(&transmissionScope); }

        ProcessorState processorState = new(
            ProcessorSystemServer.BytecodeInterpreterSettings,
            processor.ValueRW.Registers,
            new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
            new ReadOnlySpan<Instruction>(code.GetUnsafeReadOnlyPtr(), code.Length),
            ReadOnlySpan<IExternalFunction>.Empty,
            scopedExternalFunctions,
            ProcessorSystemServer.ExternalFunctionCount
        );

        for (int i = 0; i < 128; i++)
        {
            if (processorState.Registers.CodePointer == processorState.Code.Length) break;
            processorState.Process();
            if (processorState.Signal != Signal.None)
            {
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
                processorState.Registers.CodePointer = processorState.Code.Length;
                break;
            }
        }

        processor.ValueRW.Registers = processorState.Registers;
        processor.ValueRW.StatusLED.Status = 1;
    }
}
