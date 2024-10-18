using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#nullable enable

[UpdateAfter(typeof(CompilerSystem))]
partial struct ProcessorSystem : ISystem
{
    public static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };

    public static readonly IExternalFunction[] ExternalFunctions = new IExternalFunction[2]
    {
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments) =>
        {
            (float a, float b) = arguments.To<(float, float)>();
            return math.atan2(a, b).ToBytes();
        }, 1, "atan2", sizeof(float) * 2, sizeof(float)),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments) =>
        {
            return default;
        }, 2, "stdout", sizeof(char), 0),
    };

    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Processor>();
    }

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);
        using NativeHashSet<FixedString64Bytes> requestedSourceFiles = new(8, AllocatorManager.Temp);

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            if (processor.ValueRO.CompilerCache == Entity.Null)
            {
                if (!requestedSourceFiles.Add(processor.ValueRO.SourceFile)) continue;

                Debug.Log("Processor's source is null, searching for one ...");

                Entity compilerCache_ = Entity.Null;

                foreach (var (compilerCache2, compilerCache_2) in
                    SystemAPI.Query<RefRO<CompilerCache>>()
                    .WithEntityAccess())
                {
                    if (compilerCache2.ValueRO.SourceFile == processor.ValueRO.SourceFile)
                    {
                        processor.ValueRW.CompilerCache = compilerCache_2;
                        compilerCache_ = compilerCache_2;
                        Debug.Log("Source found for the processor");
                        break;
                    }
                }

                if (compilerCache_ != Entity.Null) continue;

                Debug.Log("Source not found, creating one ...");
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
                Debug.Log("Processor's source changed, reloading ...");

                ProcessorState processorState_ = new(
                    BytecodeInterpreterSettings,
                    processor.ValueRW.Registers,
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

            ExternalFunctions[1] = new ExternalFunctionSync((ReadOnlySpan<byte> arguments) =>
            {
                char output = arguments.To<char>();
                if (output == '\r') return default;
                if (output == '\n')
                {
                    Debug.Log(processor.ValueRO.StdOutBuffer.ToString());
                    processor.ValueRO.StdOutBuffer.Clear();
                    return default;
                }
                FormatError error = processor.ValueRW.StdOutBuffer.Append(output);
                if (error != FormatError.None)
                {
                    throw new RuntimeException(error.ToString());
                }
                return default;
            }, 2, "stdout", sizeof(char), 0);

            DynamicBuffer<BufferedInstruction> generated = SystemAPI.GetBuffer<BufferedInstruction>(processor.ValueRO.CompilerCache);

            ProcessorState processorState = new(
                BytecodeInterpreterSettings,
                processor.ValueRW.Registers,
                new Span<byte>(Unsafe.AsPointer(ref processor.ValueRW.Memory), 510),
                new ReadOnlySpan<Instruction>(generated.GetUnsafeReadOnlyPtr(), generated.Length),
                ExternalFunctions
            );

            for (int i = 0; i < 128; i++)
            {
                processorState.Tick();
            }

            processor.ValueRW.Registers = processorState.Registers;
        }

        entityCommandBuffer.Playback(state.EntityManager);
        entityCommandBuffer.Dispose();
    }
}
