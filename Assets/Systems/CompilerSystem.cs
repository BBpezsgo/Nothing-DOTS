using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#nullable enable

partial struct CompilerSystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CompilerCache>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (compilerCache, entity) in
            SystemAPI.Query<RefRW<CompilerCache>>()
            .WithEntityAccess())
        {
            if (compilerCache.ValueRO.CompileSecuedued)
            {
                Debug.Log("Compilation secuedued for source ...");

                compilerCache.ValueRW.CompileSecuedued = false;
                compilerCache.ValueRW.Version = File.GetLastWriteTimeUtc(compilerCache.ValueRO.SourceFile.ToString());
                compilerCache.ValueRW.HotReloadAt = Time.time + 5f;

                List<IExternalFunction> externalFunctions = new();
                externalFunctions.AddExternalFunction("stdout", (char output) => { });
                externalFunctions.AddExternalFunction<float, float, float>("atan2", math.atan2);
                CompilerResult compiled = Compiler.CompileFile(
                    new Uri(compilerCache.ValueRO.SourceFile.ToString(), UriKind.Absolute),
                    ProcessorSystem.ExternalFunctions,
                    new CompilerSettings()
                    {
                        BasePath = null,
                    },
                    LanguageCore.PreprocessorVariables.Normal
                );
                BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default);

                DynamicBuffer<BufferedInstruction> buffer = SystemAPI.GetBuffer<BufferedInstruction>(entity);

                buffer.ResizeUninitialized(generated.Code.Length);
                buffer.CopyFrom(generated.Code.Select(v => new BufferedInstruction(v)).ToArray());

                return;
            }

            if (Time.time > compilerCache.ValueRO.HotReloadAt)
            {
                compilerCache.ValueRW.HotReloadAt = Time.time + 5f;
                DateTime lastWriteTime = File.GetLastWriteTimeUtc(compilerCache.ValueRO.SourceFile.ToString());
                if (lastWriteTime != compilerCache.ValueRO.Version)
                {
                    Debug.Log("Source changed, requesting compilation ...");
                    compilerCache.ValueRW.CompileSecuedued = true;
                    compilerCache.ValueRW.Version = lastWriteTime;
                    return;
                }
            }
        }
    }
}
