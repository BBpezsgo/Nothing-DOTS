using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(UnitProcessorSystem))]
partial struct ProcessorSystemServer : ISystem
{
    public static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };
    public static unsafe readonly IExternalFunction[] ExternalFunctions = new IExternalFunction[]
    {
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            (float a, float b) = ExternalFunctionGenerator.TakeParameters<float, float>(arguments);
            float r = math.atan2(a, b);
            r.AsBytes().CopyTo(returnValue);
        }, 10, "atan2", ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.sin(a);
            r.AsBytes().CopyTo(returnValue);
        }, 11, "sin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.cos(a);
            r.AsBytes().CopyTo(returnValue);
        }, 12, "cos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.tan(a);
            r.AsBytes().CopyTo(returnValue);
        }, 13, "tan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.asin(a);
            r.AsBytes().CopyTo(returnValue);
        }, 14, "asin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.acos(a);
            r.AsBytes().CopyTo(returnValue);
        }, 15, "acos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.atan(a);
            r.AsBytes().CopyTo(returnValue);
        }, 16, "atan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.TakeParameters<float>(arguments);
            float r = math.sqrt(a);
            r.AsBytes().CopyTo(returnValue);
        }, 17, "sqrt", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 2, ExternalFunctionNames.StdOut, ExternalFunctionGenerator.SizeOf<char>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 18, "send", ExternalFunctionGenerator.SizeOf<int, int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 19, "receive", ExternalFunctionGenerator.SizeOf<int, int, int>(), ExternalFunctionGenerator.SizeOf<int>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 20, "radar", ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 21, "debug", ExternalFunctionGenerator.SizeOf<float2, int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 22, "ldebug", ExternalFunctionGenerator.SizeOf<float2, int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 23, "toglobal", ExternalFunctionGenerator.SizeOf<int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 24, "tolocal", ExternalFunctionGenerator.SizeOf<int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 25, "time", 0, ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 26, "print_float", ExternalFunctionGenerator.SizeOf<float>(), 0),
    };

    ComponentLookup<Processor> processorLookup;

    unsafe ref struct FunctionScope
    {
        public required RefRW<Processor> Processor;
        public required void* Memory;
        public required SystemState State;
        public required Entity SourceEntity;
        public required float3 SourcePosition;
    }

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Processor>();
        processorLookup = state.GetComponentLookup<Processor>();
    }

    // [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;

        processorLookup.Update(ref state);

        static void _stdout(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            char output = arguments.To<char>();
            if (output == '\r') return;
            ((FunctionScope*)_scope)->Processor.ValueRW.StdOutBuffer.AppendShift(output);
        }

        static void _send(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            (int bufferPtr, int length) = ExternalFunctionGenerator.TakeParameters<int, int>(arguments);
            if (bufferPtr <= 0 || length <= 0) return;
            if (length >= 30) throw new Exception($"Can't");

            FunctionScope* scope = (FunctionScope*)_scope;

            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            ReadOnlySpan<byte> buffer = memory.Slice(bufferPtr, length);

            using EntityQuery q = scope->State.EntityManager.CreateEntityQuery(typeof(Processor));
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

        static void _receive(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

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

        static void _radar(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

            FunctionScope* scope = (FunctionScope*)_scope;

            CollisionWorld collisionWorld;
            using (EntityQuery collisionWorldQ = scope->State.EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton)))
            { collisionWorld = collisionWorldQ.GetSingleton<PhysicsWorldSingleton>().CollisionWorld; }

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

        static void _debug(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

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

        static void _ldebug(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

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

        static void _toglobal(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

            int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            if (ptr <= 0 || ptr <= 0) return;

            FunctionScope* scope = (FunctionScope*)_scope;
            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            float2 point = memory.Get<float2>(ptr);
            LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
            float3 transformed = transform.TransformPoint(new float3(point.x, 0f, point.y));
            memory.Set(ptr, new float2(transformed.x, transformed.z));
        }

        static void _tolocal(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            returnValue.Clear();

            int ptr = ExternalFunctionGenerator.TakeParameters<int>(arguments);
            if (ptr <= 0 || ptr <= 0) return;

            FunctionScope* scope = (FunctionScope*)_scope;
            Span<byte> memory = new(scope->Memory, Processor.UserMemorySize);
            float2 point = memory.Get<float2>(ptr);
            LocalTransform transform = scope->State.EntityManager.GetComponentData<LocalTransform>(scope->SourceEntity);
            float3 transformed = transform.InverseTransformPoint(new float3(point.x, 0f, point.y));
            memory.Set(ptr, new float2(transformed.x, transformed.z));
        }

        static void _time(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            FunctionScope* scope = (FunctionScope*)_scope;
            returnValue.Set((float)scope->State.WorldUnmanaged.Time.ElapsedTime);
        }

        static void _print_float(nint _scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
        {
            Debug.Log(arguments.To<float>());
        }

        Span<ExternalFunctionScopedSync> scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[]
        {
            new ExternalFunctionScopedSync(&_stdout, 2, ExternalFunctionGenerator.SizeOf<char>(), 0, default),
            new ExternalFunctionScopedSync(&_send, 18, ExternalFunctionGenerator.SizeOf<int, int>(), 0, default),
            new ExternalFunctionScopedSync(&_receive, 19, ExternalFunctionGenerator.SizeOf<int, int, int>(), ExternalFunctionGenerator.SizeOf<int>(), default),
            new ExternalFunctionScopedSync(&_radar, 20, ExternalFunctionGenerator.SizeOf<int>(), ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(&_debug, 21, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default),
            new ExternalFunctionScopedSync(&_ldebug, 22, ExternalFunctionGenerator.SizeOf<float2, int>(), 0, default),
            new ExternalFunctionScopedSync(&_toglobal, 23, ExternalFunctionGenerator.SizeOf<int>(), 0, default),
            new ExternalFunctionScopedSync(&_tolocal, 24, ExternalFunctionGenerator.SizeOf<int>(), 0, default),
            new ExternalFunctionScopedSync(&_time, 25, 0, ExternalFunctionGenerator.SizeOf<float>(), default),
            new ExternalFunctionScopedSync(&_print_float, 26, ExternalFunctionGenerator.SizeOf<float>(), 0, default),
        };

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            if (processor.ValueRO.SourceFile == default) continue;

            if (!CompilerManager.Instance.CompiledSources.TryGetValue(processor.ValueRO.SourceFile, out var source))
            {
                // Debug.Log($"Request source \"{processor.ValueRO.SourceFile}\" ...");
                CompilerManager.Instance.CompiledSources.Add(processor.ValueRO.SourceFile, CompiledSource.Empty(processor.ValueRO.SourceFile));
                continue;
            }

            if (!source.Code.HasValue) continue;

            if (processor.ValueRO.SourceVersion != source.Version)
            {
                processor.ValueRW.StdOutBuffer.AppendShift("Reloading ...\n");

                ProcessorState processorState_ = new(
                    BytecodeInterpreterSettings,
                    default,
                    default,
                    default,
                    default,
                    default
                );
                processorState_.Setup();
                processor.ValueRW.Registers = processorState_.Registers;
                processor.ValueRW.SourceVersion = source.Version;

                return;
            }

            if (!source.IsSuccess) continue;

            FunctionScope transmissionScope = new()
            {
                Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
                State = state,
                Processor = processor,
                SourceEntity = entity,
                SourcePosition = SystemAPI.GetComponent<LocalToWorld>(entity).Position,
            };
            scopedExternalFunctions[0].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[1].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[2].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[3].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[4].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[5].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[6].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[7].Scope = (nint)(&transmissionScope);
            scopedExternalFunctions[8].Scope = (nint)(&transmissionScope);

            ProcessorState processorState = new(
                BytecodeInterpreterSettings,
                processor.ValueRW.Registers,
                new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
                source.Code.Value.AsSpan(),
                new ReadOnlySpan<IExternalFunction>(ExternalFunctions, 0, ExternalFunctions.Length - scopedExternalFunctions.Length),
                scopedExternalFunctions
            );

            try
            {
                for (int i = 0; i < 256; i++)
                {
                    processorState.Tick();
                }
            }
            catch (RuntimeException exception)
            {
                exception.DebugInformation = source.DebugInformation;
                Debug.LogError(exception.ToString(false));
            }

            processor.ValueRW.Registers = processorState.Registers;
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }
}
