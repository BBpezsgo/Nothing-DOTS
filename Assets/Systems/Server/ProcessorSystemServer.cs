using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
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
    static readonly ProfilerMarker _ProcessMarker = new("ProcessorSystemServer.Process");
    static readonly ProfilerMarker _ExternalMarker_stdout = new("ProcessorSystemServer.External.stdout");
    static readonly ProfilerMarker _ExternalMarker_send = new("ProcessorSystemServer.External.send");
    static readonly ProfilerMarker _ExternalMarker_receive = new("ProcessorSystemServer.External.receive");
    static readonly ProfilerMarker _ExternalMarker_radar = new("ProcessorSystemServer.External.radar");
    static readonly ProfilerMarker _ExternalMarker_debug = new("ProcessorSystemServer.External.debug");
    static readonly ProfilerMarker _ExternalMarker_time = new("ProcessorSystemServer.External.time");
    static readonly ProfilerMarker _ExternalMarker_math = new("ProcessorSystemServer.External.math");
    static readonly ProfilerMarker _ExternalMarker_other = new("ProcessorSystemServer.External.other");

    public static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };

    [BurstCompile]
    ref struct FunctionScope
    {
        public required RefRW<Processor> Processor;
        public required void* Memory;
        public required SystemState State;
        public required Entity SourceEntity;
        public required float3 SourcePosition;
        public required EntityQuery ProcessorEntitiesQ;
    }

    static readonly Unity.Mathematics.Random _sharedRandom = Unity.Mathematics.Random.CreateFromIndex(420);

    #region Processor Math Accelerators

    [BurstCompile]
    static void _atan2(nint scope, nint arguments, nint returnValue)
    {
        (float a, float b) = ExternalFunctionGenerator.TakeParameters<float, float>(arguments);
        float r = math.atan2(a, b);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _sin(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.sin(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _cos(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.cos(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _tan(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.tan(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _asin(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.asin(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _acos(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.acos(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _atan(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.atan(a);
        r.AsBytes().CopyTo(returnValue);
    }

    [BurstCompile]
    static void _sqrt(nint scope, nint arguments, nint returnValue)
    {
        float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
        float r = math.sqrt(a);
        r.AsBytes().CopyTo(returnValue);
    }

    #endregion

    #region Processor Unit Functions

    [BurstCompile]
    static void _stdout(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_stdout.Auto();

        char output = arguments.To<char>();
        if (output == '\r') return;
        ((FunctionScope*)_scope)->Processor.ValueRW.StdOutBuffer.AppendShift(output);
    }

    [BurstCompile]
    static void _send(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_send.Auto();

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

        NativeHashMap<uint, NativeList<QuadrantEntity>> map = QuadrantSystem.GetMap(scope->State.WorldUnmanaged);
        int2 grid = QuadrantSystem.ToGrid(scope->SourcePosition);

        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                if (!map.TryGetValue(QuadrantSystem.GetKey(grid + new int2(x, z)), out NativeList<QuadrantEntity> cell)) continue;
                for (int i = 0; i < cell.Length; i++)
                {
                    if (cell[i].Entity == scope->SourceEntity) continue;

                    if (angle != 0f)
                    {
                        float3 entityDirection = cell[i].Position;
                        entityDirection.x -= scope->SourcePosition.x;
                        entityDirection.y = 0f;
                        entityDirection.z -= scope->SourcePosition.z;
                        entityDirection = math.normalize(entityDirection);
                        float dot = math.abs(math.dot(direction, entityDirection));
                        if (dot < cosAngle) continue;
                    }

                    DynamicBuffer<BufferedUnitTransmission> transmissions = scope->State.EntityManager.GetBuffer<BufferedUnitTransmission>(cell[i].Entity);
                    if (transmissions.Length > 128) transmissions.RemoveAt(0);
                    transmissions.Add(new BufferedUnitTransmission(scope->SourcePosition, data));
                }
            }
        }

        return;

        using NativeArray<Entity> entities = scope->ProcessorEntitiesQ.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (entities[i] == scope->SourceEntity) continue;

            if (angle != 0f)
            {
                float3 entityDirection = scope->State.EntityManager.GetComponentData<LocalToWorld>(entities[i]).Position;
                entityDirection.x -= scope->SourcePosition.x;
                entityDirection.y = 0f;
                entityDirection.z -= scope->SourcePosition.z;
                entityDirection = math.normalize(entityDirection);
                float dot = math.abs(math.dot(direction, entityDirection));
                if (dot < cosAngle) continue;
            }

            DynamicBuffer<BufferedUnitTransmission> transmissions = scope->State.EntityManager.GetBuffer<BufferedUnitTransmission>(entities[i]);
            if (transmissions.Length > 128) transmissions.RemoveAt(0);
            transmissions.Add(new BufferedUnitTransmission(scope->SourcePosition, data));
        }
    }

    [BurstCompile]
    static void _receive(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_receive.Auto();

        returnValue.Clear(sizeof(int));

        (int bufferPtr, int length, int directionPtr, int strengthPtr) = ExternalFunctionGenerator.TakeParameters<int, int, int, int>(arguments);
        if (bufferPtr <= 0 || length <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;

        DynamicBuffer<BufferedUnitTransmission> received = scope->State.EntityManager.GetBuffer<BufferedUnitTransmission>(scope->SourceEntity);
        if (received.Length == 0) return;

        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        Span<byte> buffer = memory.Slice(bufferPtr, length);

        int copyLength = System.Math.Min(received[0].Data.Length, buffer.Length);

        for (int i = 0; i < copyLength; i++)
        {
            buffer[i] = received[0].Data[i];
        }

        scope->Processor.ValueRW.NetworkReceiveLED.Blink();

        if (directionPtr > 0)
        {
            LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
            float3 transformed = transform.InverseTransformPoint(received[0].Source);
            memory.Set(directionPtr, math.atan2(transformed.x, transformed.z));
        }

        if (strengthPtr > 0)
        {
            memory.Set(strengthPtr, (byte)255);
        }

        if (copyLength >= received[0].Data.Length)
        { received.RemoveAt(0); }
        else
        { received[0].Data.RemoveRange(0, copyLength); }

        returnValue.Set(copyLength);
    }

    [BurstCompile]
    static void _radar(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_radar.Auto();

        returnValue.Clear(sizeof(float));

        FunctionScope* scope = (FunctionScope*)_scope;

        CollisionWorld collisionWorld;
        EntityQueryBuilder collisionWorldQB = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();
        using (EntityQuery collisionWorldQ = scope->State.EntityManager.CreateEntityQuery(collisionWorldQB))
        { collisionWorld = collisionWorldQ.GetSingleton<PhysicsWorldSingleton>().CollisionWorld; }
        collisionWorldQB.Dispose();

        LocalToWorld radar = scope->State.EntityManager.GetComponentData<LocalToWorld>(scope->State.EntityManager.GetComponentData<Unit>(scope->SourceEntity).Radar);

        RaycastInput input = new()
        {
            Start = scope->SourcePosition + (math.normalize(radar.Forward) * 1f),
            End = scope->SourcePosition + (math.normalize(radar.Forward) * 80f),
            Filter = new CollisionFilter()
            {
                BelongsTo = Layers.All,
                CollidesWith = Layers.All,
                GroupIndex = 0,
            },
        };

        if (!collisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        { return; }

        scope->Processor.ValueRW.RadarLED.Blink();

        returnValue.Set(math.distance(hit.Position, input.Start) + 1f);
    }

    [BurstCompile]
    static void _debug(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();

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
    }

    [BurstCompile]
    static void _ldebug(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_debug.Auto();

        (float2 position, int color) = ExternalFunctionGenerator.TakeParameters<float2, int>(arguments);

        FunctionScope* scope = (FunctionScope*)_scope;

        LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
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
    }

    [BurstCompile]
    static void _toglobal(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();

        int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (ptr <= 0 || ptr <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;
        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        float2 point = memory.Get<float2>(ptr);
        LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
        float3 transformed = transform.TransformPoint(new float3(point.x, 0f, point.y));
        memory.Set(ptr, new float2(transformed.x, transformed.z));
    }

    [BurstCompile]
    static void _tolocal(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();

        int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
        if (ptr <= 0 || ptr <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;
        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        float2 point = memory.Get<float2>(ptr);
        LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
        float3 transformed = transform.InverseTransformPoint(new float3(point.x, 0f, point.y));
        memory.Set(ptr, new float2(transformed.x, transformed.z));
    }

    [BurstCompile]
    static void _time(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_time.Auto();

        FunctionScope* scope = (FunctionScope*)_scope;
        returnValue.Set((float)scope->State.WorldUnmanaged.Time.ElapsedTime);
    }

    [BurstCompile]
    static void _random(nint _scope, nint arguments, nint returnValue)
    {
        using ProfilerMarker.AutoScope marker = _ExternalMarker_other.Auto();

        returnValue.Set(_sharedRandom.NextInt());
    }

    #endregion

    public const int ExternalFunctionCount = 18;

    [BurstCompile]
    public static void GenerateExternalFunctions(ExternalFunctionScopedSync* buffer)
    {
        int i = 0;

        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_stdout), 01, ExternalFunctionGenerator.SizeOf<char>(), 0, default);

        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_sqrt), 11, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_atan2), 12, ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_sin), 13, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_cos), 14, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_tan), 15, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_asin), 16, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_acos), 17, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_atan), 18, ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default);

        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_send), 21, ExternalFunctionGenerator.SizeOf<int, int, float, float>(), 0, default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_receive), 22, ExternalFunctionGenerator.SizeOf<int, int, int, int>(), ExternalFunctionGenerator.SizeOf<int>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_radar), 23, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<float>(), default);

        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_toglobal), 31, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_tolocal), 32, ExternalFunctionGenerator.SizeOf<int>(), 0, default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_time), 33, 0, ExternalFunctionGenerator.SizeOf<float>(), default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_random), 34, 0, ExternalFunctionGenerator.SizeOf<int>(), default);

        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_debug), 41, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default);
        buffer[i++] = new(BurstCompiler.CompileFunctionPointer(_ldebug), 42, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default);

        Unity.Assertions.Assert.AreEqual(i, ExternalFunctionCount);
    }

    EntityQuery processorEntitiesQ;

    void ISystem.OnCreate(ref SystemState state)
    {
        processorEntitiesQ = state.GetEntityQuery(typeof(Processor));
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        ExternalFunctionScopedSync* scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[ExternalFunctionCount];
        GenerateExternalFunctions(scopedExternalFunctions);

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
                State = state,
                Processor = processor,
                SourceEntity = entity,
                SourcePosition = transform.ValueRO.Position,
                ProcessorEntitiesQ = processorEntitiesQ,
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

            using (ProfilerMarker.AutoScope marker = _ProcessMarker.Auto())
            {
                for (int i = 0; i < 256; i++)
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
    }
}
