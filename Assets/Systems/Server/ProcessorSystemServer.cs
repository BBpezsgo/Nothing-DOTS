using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(CompilerSystemServer))]
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
            (float a, float b) = ExternalFunctionGenerator.DeconstructValues<float, float>(arguments);
            float r = math.atan2(a, b);
            r.AsBytes().CopyTo(returnValue);
        }, 10, "atan2", ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.sin(a);
            r.AsBytes().CopyTo(returnValue);
        }, 11, "sin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.cos(a);
            r.AsBytes().CopyTo(returnValue);
        }, 12, "cos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.tan(a);
            r.AsBytes().CopyTo(returnValue);
        }, 13, "tan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.asin(a);
            r.AsBytes().CopyTo(returnValue);
        }, 14, "asin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.acos(a);
            r.AsBytes().CopyTo(returnValue);
        }, 15, "acos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.atan(a);
            r.AsBytes().CopyTo(returnValue);
        }, 16, "atan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            float a = ExternalFunctionGenerator.DeconstructValues<float>(arguments);
            float r = math.sqrt(a);
            r.AsBytes().CopyTo(returnValue);
        }, 17, "sqrt", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            return;
        }, 2, ExternalFunctionNames.StdOut, ExternalFunctionGenerator.SizeOf<char>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            return;
        }, 18, "send", ExternalFunctionGenerator.SizeOf<int, int>(), 0),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        {
            return;
        }, 19, "receive", ExternalFunctionGenerator.SizeOf<int, int>(), ExternalFunctionGenerator.SizeOf<int>()),
    };
    private ComponentLookup<Processor> processorLookup;

    unsafe ref struct TransmissionScope
    {
        public RefRW<Processor> Processor;
        public void* Memory;
        public SystemState State;
        public Entity SourceEntity;
        public float3 SourcePosition;
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
        NativeHashSet<FileId> requestedSourceFiles = default;

        processorLookup.Update(ref state);

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            if (processor.ValueRO.CompilerCache == Entity.Null)
            {
                if (processor.ValueRO.SourceFile == default) continue;
                if (!requestedSourceFiles.IsCreated) requestedSourceFiles = new(8, AllocatorManager.Temp);
                if (!requestedSourceFiles.Add(processor.ValueRO.SourceFile)) continue;

                // Debug.Log("Processor's source is null, searching for one ...");

                Entity compilerCache_ = Entity.Null;

                foreach (var (compilerCache2, compilerCache_2) in
                    SystemAPI.Query<RefRO<CompilerCache>>()
                    .WithEntityAccess())
                {
                    if (compilerCache2.ValueRO.SourceFile == processor.ValueRO.SourceFile)
                    {
                        processor.ValueRW.CompilerCache = compilerCache_2;
                        compilerCache_ = compilerCache_2;
                        // Debug.Log("Source found for the processor");
                        break;
                    }
                }

                if (compilerCache_ != Entity.Null) continue;

                // Debug.Log("Source not found, creating one ...");
                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                compilerCache_ = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(compilerCache_, new CompilerCache()
                {
                    SourceFile = processor.ValueRO.SourceFile,
                    CompileSecuedued = 1d,
                    Version = default,
                });
                entityCommandBuffer.AddBuffer<BufferedInstruction>(compilerCache_);
                entityCommandBuffer.AddBuffer<BufferedCompilationAnalystics>(compilerCache_);
                continue;
            }

            RefRO<CompilerCache> compilerCache = SystemAPI.GetComponentRO<CompilerCache>(processor.ValueRO.CompilerCache);

            if (processor.ValueRO.SourceVersion != compilerCache.ValueRO.Version)
            {
                // Debug.Log("Processor's source changed, reloading ...");

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
                processor.ValueRW.SourceVersion = compilerCache.ValueRO.Version;

                // Dictionary<int, IExternalFunction> externalFunctions2 = new();
                // externalFunctions2.AddExternalFunction("sleep", (int miliseconds) =>
                // {
                //     processor.ValueRW.SleepUntil = Time.time + (miliseconds / 1000f);
                // });
                // externalFunctions2.AddExternalFunction("stdout", (char output) =>
                // {
                //     if (output == '\r') return;
                //     if (output == '\n')
                //     {
                //         Debug.Log(processor.ValueRO.StdOutBuffer.ToString());
                //         processor.ValueRO.StdOutBuffer.Clear();
                //         return;
                //     }
                //     FormatError error = processor.ValueRW.StdOutBuffer.Append(output);
                //     if (error != FormatError.None)
                //     {
                //         throw new RuntimeException(error.ToString());
                //     }
                // });
                // externalFunctions2.AddExternalFunction<float, float, float>("atan2", math.atan2);

                // DynamicBuffer<BufferedInstruction> generated = SystemAPI.GetBuffer<BufferedInstruction>(processor.ValueRO.CompilerCache);
                // processor.ValueRW.BytecodeProcessor = new BytecodeProcessor(
                //     ImmutableArray.Create(new ReadOnlySpan<Instruction>(generated.GetUnsafeReadOnlyPtr(), generated.Length)),
                //     new byte[
                //         BytecodeInterpreterSettings.HeapSize +
                //         BytecodeInterpreterSettings.StackSize +
                //         128
                //     ],
                //     externalFunctions.ToFrozenDictionary(),
                //     BytecodeInterpreterSettings
                // );
                return;
            }

            void* stdoutBufferPtr = Unsafe.AsPointer(ref processor.ValueRW.StdOutBuffer);
            static void _stdout(nint scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
            {
                char output = arguments.To<char>();
                if (output == '\r') return;
                ((FixedString128Bytes*)scope)->AppendShift(output);
            }

            TransmissionScope transmissionScope = new()
            {
                Memory = Unsafe.AsPointer(ref processor.ValueRW.Memory),
                State = state,
                Processor = processor,
                SourceEntity = entity,
                SourcePosition = SystemAPI.GetComponent<Unity.Transforms.LocalToWorld>(entity).Position,
            };
            TransmissionScope* transmissionScopePtr = &transmissionScope;
            static void _send(nint scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
            {
                (int bufferPtr, int length) = ExternalFunctionGenerator.DeconstructValues<int, int>(arguments);
                if (bufferPtr <= 0 || length <= 0) return;
                if (length >= 30) throw new Exception($"Can't");
                ReadOnlySpan<byte> buffer = new(((TransmissionScope*)scope)->Memory, Processor.UserMemorySize);
                buffer = buffer.Slice(bufferPtr, length);
                using EntityQuery q = ((TransmissionScope*)scope)->State.EntityManager.CreateEntityQuery(typeof(Processor));
                using NativeArray<Entity> entities = q.ToEntityArray(Allocator.Temp);
                fixed (byte* bufferPtr2 = buffer)
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        if (entities[i] == ((TransmissionScope*)scope)->SourceEntity) continue;
                        FixedList32Bytes<byte> data = new();
                        data.AddRange(bufferPtr2, length);
                        DynamicBuffer<BufferedTransmittedUnitData> transmissions = ((TransmissionScope*)scope)->State.EntityManager.GetBuffer<BufferedTransmittedUnitData>(entities[i]);
                        transmissions.Add(new BufferedTransmittedUnitData(((TransmissionScope*)scope)->SourcePosition, data));
                    }
                }
            }
            static void _receive(nint scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
            {
                returnValue.Clear();
                (int bufferPtr, int length) = ExternalFunctionGenerator.DeconstructValues<int, int>(arguments);
                if (bufferPtr <= 0 || length <= 0) return;
                Span<byte> buffer = new(((TransmissionScope*)scope)->Memory, Processor.UserMemorySize);
                buffer = buffer.Slice(bufferPtr, length);
                DynamicBuffer<BufferedTransmittedUnitData> received = ((TransmissionScope*)scope)->State.EntityManager.GetBuffer<BufferedTransmittedUnitData>(((TransmissionScope*)scope)->SourceEntity);
                int k = 0;
                int receivedChunks = 0;
                for (int i = 0; i < received.Length; i++)
                {
                    if (k >= length) break;
                    int receivedChunkBytes = 0;
                    for (int j = 0; j < received[i].Data.Length; j++)
                    {
                        if (k >= length) break;
                        receivedChunkBytes++;
                        buffer[k] = received[i].Data[j];
                        k++;
                    }
                    if (receivedChunkBytes >= received[i].Data.Length)
                    {
                        receivedChunks++;
                    }
                }
                if (receivedChunks > 0) received.RemoveRange(0, receivedChunks);
                returnValue.Set(k);
            }

            Span<ExternalFunctionScopedSync> scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[]
            {
                new ExternalFunctionScopedSync(&_stdout, 2, ExternalFunctionGenerator.SizeOf<char>(), 0, stdoutBufferPtr),
                new ExternalFunctionScopedSync(&_send, 18, ExternalFunctionGenerator.SizeOf<int, int>(), 0, transmissionScopePtr),
                new ExternalFunctionScopedSync(&_receive, 19, ExternalFunctionGenerator.SizeOf<int, int>(), ExternalFunctionGenerator.SizeOf<int>(), transmissionScopePtr),
            };

            DynamicBuffer<BufferedInstruction> generated = SystemAPI.GetBuffer<BufferedInstruction>(processor.ValueRO.CompilerCache);

            ProcessorState processorState = new(
                BytecodeInterpreterSettings,
                processor.ValueRW.Registers,
                new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), Processor.TotalMemorySize),
                new ReadOnlySpan<Instruction>(generated.GetUnsafeReadOnlyPtr(), generated.Length),
                new ReadOnlySpan<IExternalFunction>(ExternalFunctions, 0, ExternalFunctions.Length - scopedExternalFunctions.Length),
                scopedExternalFunctions
            );

            for (int i = 0; i < 128; i++)
            {
                processorState.Tick();
            }

            processor.ValueRW.Registers = processorState.Registers;
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
        if (requestedSourceFiles.IsCreated)
        {
            requestedSourceFiles.Dispose();
        }
    }
}
