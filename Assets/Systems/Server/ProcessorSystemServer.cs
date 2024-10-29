using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(UnitProcessorSystem))]
[BurstCompile]
unsafe partial struct ProcessorSystemServer : ISystem
{
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
    }

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
        char output = arguments.To<char>();
        if (output == '\r') return;
        ((FunctionScope*)_scope)->Processor.ValueRW.StdOutBuffer.AppendShift(output);
    }

    [BurstCompile]
    static void _send(nint _scope, nint arguments, nint returnValue)
    {
        (int bufferPtr, int length) = ExternalFunctionGenerator.TakeParameters<int, int>(arguments);
        if (bufferPtr <= 0 || length <= 0) return;
        if (length >= 30) throw new Exception($"Can't");

        FunctionScope* scope = (FunctionScope*)_scope;

        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        ReadOnlySpan<byte> buffer = memory.Slice(bufferPtr, length);

        EntityQueryBuilder qb = new EntityQueryBuilder(Allocator.Temp).WithAll<Processor>();
        using EntityQuery q = scope->State.EntityManager.CreateEntityQuery(qb);
        qb.Dispose();
        using NativeArray<Entity> entities = q.ToEntityArray(Allocator.Temp);
        fixed (byte* bufferPtr2 = buffer)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == scope->SourceEntity) continue;
                FixedList32Bytes<byte> data = new();
                data.AddRange(bufferPtr2, length);
                DynamicBuffer<BufferedTransmittedUnitData> transmissions = scope->State.EntityManager.GetBuffer<BufferedTransmittedUnitData>(entities[i]);
                transmissions.Add(new BufferedTransmittedUnitData(scope->SourcePosition, data));
            }
        }
    }

    [BurstCompile]
    static void _receive(nint _scope, nint arguments, nint returnValue)
    {
        returnValue.Clear(sizeof(int));

        (int bufferPtr, int length, int directionPtr) = ExternalFunctionGenerator.TakeParameters<int, int, int>(arguments);
        if (bufferPtr <= 0 || length <= 0) return;

        FunctionScope* scope = (FunctionScope*)_scope;

        Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
        Span<byte> buffer = memory.Slice(bufferPtr, length);

        DynamicBuffer<BufferedTransmittedUnitData> received = scope->State.EntityManager.GetBuffer<BufferedTransmittedUnitData>(scope->SourceEntity);
        if (received.Length == 0) return;

        int i;
        for (i = 0; i < received[0].Data.Length && i < buffer.Length; i++)
        {
            buffer[i] = received[0].Data[i];
        }

        if (directionPtr > 0)
        {
            LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
            float3 transformed = transform.InverseTransformPoint(received[0].Source);
            // float2 selfXZ = new(scope->SourcePosition.x, scope->SourcePosition.z);
            // float2 sourceXZ = new(received[0].Source.x, received[0].Source.z);
            // memory.Set(directionPtr, sourceXZ - selfXZ);
            memory.Set(directionPtr, new float2(transformed.x, transformed.z));
        }

        if (i >= received[0].Data.Length)
        { received.RemoveAt(0); }
        else
        { received[0].Data.RemoveRange(0, i); }

        returnValue.Set(i);
    }

    [BurstCompile]
    static void _radar(nint _scope, nint arguments, nint returnValue)
    {
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
                BelongsTo = ~0u,
                CollidesWith = ~0u,
                GroupIndex = 0,
            },
        };

        if (!collisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        { return; }

        returnValue.Set(math.distance(hit.Position, input.Start) + 1f);
    }

    [BurstCompile]
    static void _debug(nint _scope, nint arguments, nint returnValue)
    {
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
        FunctionScope* scope = (FunctionScope*)_scope;
        returnValue.Set((float)scope->State.WorldUnmanaged.Time.ElapsedTime);
    }

    #endregion

    const int ExternalFunctionCount = 17;

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        ExternalFunctionScopedSync* scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[ExternalFunctionCount]
        {
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_atan2), 10, ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_sin), 11,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_cos), 12,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_tan), 13,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_asin), 14,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_acos), 15,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_atan), 16,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_sqrt), 17,ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_stdout), 2, ExternalFunctionGenerator.SizeOf<char>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_send), 18, ExternalFunctionGenerator.SizeOf<int, int>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_receive), 19, ExternalFunctionGenerator.SizeOf<int, int, int>(), ExternalFunctionGenerator.SizeOf<int>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_radar), 20, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_debug), 21, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_ldebug), 22, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_toglobal), 23, ExternalFunctionGenerator.SizeOf<int>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_tolocal), 24, ExternalFunctionGenerator.SizeOf<int>(), 0, default),
            new ExternalFunctionScopedSync(BurstCompiler.CompileFunctionPointer(_time), 25, 0, ExternalFunctionGenerator.SizeOf<float>(), default),
        };

        foreach ((RefRW<Processor> processor, Entity entity) in
            SystemAPI.Query<RefRW<Processor>>()
            .WithEntityAccess())
        {
            if (processor.ValueRO.SourceFile == default) continue;

            DynamicBuffer<BufferedInstruction> code = SystemAPI.GetBuffer<BufferedInstruction>(entity);

            if (code.IsEmpty) continue;

            FunctionScope transmissionScope = new()
            {
                Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
                State = state,
                Processor = processor,
                SourceEntity = entity,
                SourcePosition = SystemAPI.GetComponent<LocalToWorld>(entity).Position,
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

            for (int i = 0; i < 256; i++)
            {
                if (processorState.Registers.CodePointer == processorState.Code.Length) break;
                processorState.Process();
                if (processorState.Signal != Signal.None)
                {
                    Debug.LogError("Crashed");
                    processorState.Registers.CodePointer = processorState.Code.Length;
                    break;
                }
            }

            processor.ValueRW.Registers = processorState.Registers;
        }
    }
}
