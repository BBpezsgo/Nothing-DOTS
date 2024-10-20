using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(CompilerSystem))]
partial struct ProcessorSystem : ISystem
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
            Debug.Log(a);
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
    };

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Processor>();
    }

    // [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;
        NativeHashSet<FixedString64Bytes> requestedSourceFiles = default;

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            if (processor.ValueRO.CompilerCache == Entity.Null)
            {
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
                    CompileSecuedued = true,
                    Version = default,
                });
                entityCommandBuffer.AddBuffer<BufferedInstruction>(compilerCache_);
                continue;
            }

            RefRO<CompilerCache> compilerCache = SystemAPI.GetComponentRO<CompilerCache>(processor.ValueRO.CompilerCache);

            if (processor.ValueRO.SourceVersion != compilerCache.ValueRO.Version)
            {
                // Debug.Log("Processor's source changed, reloading ...");

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

            static void _StdOut(nint scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue)
            {
                char output = arguments.To<char>();
                if (output == '\r') return;
                ((FixedString128Bytes*)scope)->AppendShift(output);
            }

            void* stdoutBufferPtr = Unsafe.AsPointer(ref processor.ValueRW.StdOutBuffer);
            Span<ExternalFunctionScopedSync> scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[]
            {
                new ExternalFunctionScopedSync(&_StdOut, 2, sizeof(char), 0, (nint)stdoutBufferPtr)
            };

            DynamicBuffer<BufferedInstruction> generated = SystemAPI.GetBuffer<BufferedInstruction>(processor.ValueRO.CompilerCache);

            ProcessorState processorState = new(
                BytecodeInterpreterSettings,
                processor.ValueRW.Registers,
                new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), 510),
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
