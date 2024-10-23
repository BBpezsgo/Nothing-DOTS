using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct CompilerSystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CompilerCache>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);

        foreach (var (compilerCache, entity) in
            SystemAPI.Query<RefRW<CompilerCache>>()
            .WithEntityAccess())
        {
            if (compilerCache.ValueRO.CompileSecuedued != default)
            {
                if (compilerCache.ValueRO.CompileSecuedued > SystemAPI.Time.ElapsedTime) continue;

                Debug.Log("Compilation secuedued for source ...");

                Uri baseUri = new($"netcode://{compilerCache.ValueRO.SourceFile.Source.Index}_{compilerCache.ValueRO.SourceFile.Source.Version}", UriKind.Absolute);
                Uri sourceUri = new(baseUri, compilerCache.ValueRO.SourceFile.Name.ToString());
                // Uri sourceUri = new(Path.Combine(FileChunkManager.BasePath, compilerCache.ValueRO.SourceFile.Name.ToString()), UriKind.Absolute),
                // Debug.Log(sourceUri);

                double now = SystemAPI.Time.ElapsedTime;
                bool sourcesFromOtherConnectionsNeeded = false;
                try
                {
                    CompilerResult compiled = Compiler.CompileFile(
                        sourceUri,
                        ProcessorSystem.ExternalFunctions,
                        new CompilerSettings()
                        {
                            BasePath = null,
                        },
                        LanguageCore.PreprocessorVariables.Normal,
                        null,
                        null,
                        null,
                        new FileParser((Uri uri, TokenizerSettings? tokenizerSettings, out ParserResult parserResult) =>
                        {
                            parserResult = default;

                            if (uri.Scheme != "netcode")
                            { return false; }

                            if (!uri.Host.Contains('_'))
                            { return false; }

                            if (!int.TryParse(uri.Host.Split('_')[0], out var entityIndex))
                            { return false; }

                            if (!int.TryParse(uri.Host.Split('_')[1], out var entityVersion))
                            { return false; }

                            Entity entity = new() { Index = entityIndex, Version = entityVersion };

                            string path = uri.AbsolutePath;
                            if (path.StartsWith("/~"))
                            {
                                path = path[1..];
                            }

                            FileStatus status = FileChunkManager.TryGetFile(new FileId(new FixedString64Bytes(path), entity), out var data);

                            if (status == FileStatus.Received)
                            {
                                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(data.Data), LanguageCore.PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                                return true;
                            }

                            sourcesFromOtherConnectionsNeeded = true;

                            if (status == FileStatus.Receiving)
                            {
                                return false;
                            }

                            FileChunkManager.TryGetFile(new FileId(path, entity), (data) =>
                            {
                                // Debug.Log($"Source \"{path}\" downloaded ...\n{Encoding.UTF8.GetString(data)}");
                            }, entityCommandBuffer);
                            compilerCache.ValueRW.CompileSecuedued = now + 5d;
                            // Debug.Log($"Source needs file \"{path}\" ...");

                            return false;
                        })
                    );
                    BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default);

                    DynamicBuffer<BufferedInstruction> buffer = SystemAPI.GetBuffer<BufferedInstruction>(entity);

                    buffer.ResizeUninitialized(generated.Code.Length);
                    buffer.CopyFrom(generated.Code.Select(v => new BufferedInstruction(v)).ToArray());

                    compilerCache.ValueRW.CompileSecuedued = default;
                    compilerCache.ValueRW.Version = DateTime.UtcNow.Ticks; // compilerCache.ValueRO.SourceFile.Version;
                    // compilerCache.ValueRW.Version = File.GetLastWriteTimeUtc(compilerCache.ValueRO.SourceFile.ToString()).Ticks;
                    // compilerCache.ValueRW.HotReloadAt = Time.time + 5f;
                }
                catch (Exception exception)
                {
                    if (!sourcesFromOtherConnectionsNeeded)
                    {
                        Debug.LogWarning(exception);
                    }
                }
            }

            // if (Time.time > compilerCache.ValueRO.HotReloadAt)
            // {
            //     compilerCache.ValueRW.HotReloadAt = Time.time + 5f;
            //     DateTime lastWriteTime = File.GetLastWriteTimeUtc(compilerCache.ValueRO.SourceFile.ToString());
            //     if (lastWriteTime.Ticks != compilerCache.ValueRO.Version)
            //     {
            //         // Debug.Log("Source changed, requesting compilation ...");
            //         compilerCache.ValueRW.CompileSecuedued = true;
            //         compilerCache.ValueRW.Version = lastWriteTime.Ticks;
            //         continue;
            //     }
            // }
        }

        entityCommandBuffer.Playback(state.EntityManager);
    }
}
