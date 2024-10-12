using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Entities;
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
                processor.CompileSecuedued = false;
                Dictionary<int, IExternalFunction> externalFunctions = new();
                externalFunctions.AddExternalFunction("sleep", (int miliseconds) =>
                {
                    processor.SleepUntil = DateTime.UtcNow.TimeOfDay.TotalSeconds + ((double)miliseconds / 1000d);
                });
                externalFunctions.AddExternalFunction("printf", (float v) => Debug.Log(v));
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
                        32
                    ],
                    externalFunctions.ToFrozenDictionary(),
                    BytecodeInterpreterSettings
                );
            }
            else if (processor.BytecodeProcessor is not null &&
                     processor.SleepUntil < DateTime.UtcNow.TimeOfDay.TotalSeconds)
            {
                processor.BytecodeProcessor.Tick();
            }
        }
    }
}
