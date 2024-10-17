using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#nullable enable

partial struct ProcessorSystem : ISystem
{
    static readonly BytecodeInterpreterSettings BytecodeInterpreterSettings = new()
    {
        HeapSize = Processor.HeapSize,
        StackSize = Processor.StackSize,
    };

    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach ((Processor processor, Entity entity) in
                    SystemAPI.Query<Processor>()
                    .WithEntityAccess())
        {
            if (processor.CompileSecuedued)
            {
                Debug.Log("Compiling ...");

                processor.CompileSecuedued = false;
                processor.SourceVersion = File.GetLastWriteTimeUtc(processor.SourceFile.ToString());
                processor.HotReloadAt = Time.time + 5f;

                Dictionary<int, IExternalFunction> externalFunctions = new();
                externalFunctions.AddExternalFunction("sleep", (int miliseconds) =>
                {
                    processor.SleepUntil = Time.time + (miliseconds / 1000f);
                });
                externalFunctions.AddExternalFunction("stdout", (char output) =>
                {
                    if (output == '\r') return;
                    if (output == '\n')
                    {
                        Debug.Log(processor.StdOutBuffer.ToString());
                        processor.StdOutBuffer.Clear();
                        return;
                    }
                    FormatError error = processor.StdOutBuffer.Append(output);
                    if (error != FormatError.None)
                    {
                        throw new RuntimeException(error.ToString());
                    }
                });
                externalFunctions.AddExternalFunction<float>("printf", (float v) => Debug.Log(v));
                externalFunctions.AddExternalFunction<float, float, float>("atan2", math.atan2);
                CompilerResult compiled = Compiler.CompileFile(
                    new Uri(processor.SourceFile.ToString(), UriKind.Absolute),
                    externalFunctions,
                    new CompilerSettings()
                    {
                        BasePath = null,
                    },
                    LanguageCore.PreprocessorVariables.Normal
                );
                BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default);
                processor.BytecodeProcessor = new BytecodeProcessor(
                    generated.Code,
                    new byte[
                        BytecodeInterpreterSettings.HeapSize +
                        BytecodeInterpreterSettings.StackSize +
                        128
                    ],
                    externalFunctions.ToFrozenDictionary(),
                    BytecodeInterpreterSettings
                );
                return;
            }

            if (Time.time > processor.HotReloadAt)
            {
                processor.HotReloadAt = Time.time + 5f;
                DateTime lastWriteTime = File.GetLastWriteTimeUtc(processor.SourceFile.ToString());
                if (lastWriteTime != processor.SourceVersion)
                {
                    Debug.Log("Source files changed, hot reloading ...");
                    processor.CompileSecuedued = true;
                    processor.SourceVersion = lastWriteTime;
                    return;
                }
            }

            if (processor.BytecodeProcessor is not null &&
                processor.SleepUntil < Time.time)
            {
                processor.SleepUntil = default;
                for (int i = 0; i < 128; i++)
                {
                    if (processor.BytecodeProcessor.IsDone &&
                        processor.SleepUntil != default) break;
                    processor.BytecodeProcessor.Tick();
                }
            }
        }
    }
}
