using System;
using System.Linq;
using System.Text;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;
using Maths;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct CompilerSystemServer : ISystem
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

                // Debug.Log("Compilation secuedued for source ...");

                Uri sourceUri = compilerCache.ValueRW.SourceFile.ToUri();

                // if (sourceUri.TryGetNetcode(out var n))
                // {
                //     Debug.Log($"{compilerCache.ValueRW.SourceFile.Name} -> {sourceUri} -> {n.Name}");
                // }

                double now = SystemAPI.Time.ElapsedTime;
                bool sourcesFromOtherConnectionsNeeded = false;
                AnalysisCollection analysisCollection = new();

                compilerCache.ValueRW.DownloadingFiles = 0;
                compilerCache.ValueRW.DownloadedFiles = 0;
                compilerCache.ValueRW.IsSuccess = false;

                try
                {
                    CompilerResult compiled = Compiler.CompileFile(
                        sourceUri,
                        ProcessorSystemServer.ExternalFunctions,
                        new CompilerSettings()
                        {
                            BasePath = null,
                        },
                        PreprocessorVariables.Normal,
                        null,
                        analysisCollection,
                        null,
                        new FileParser((Uri uri, TokenizerSettings? tokenizerSettings, out ParserResult parserResult) =>
                        {
                            parserResult = default;

                            if (!uri.TryGetNetcode(out FileId file)) return false;

                            if (file.Source == default)
                            {
                                var localFile = FileChunkManager.GetLocalFile(file.Name.ToString());
                                if (!localFile.HasValue) return false;

                                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(localFile.Value.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                                return true;
                            }

                            FileStatus status = FileChunkManager.TryGetFile(file, out var data);

                            compilerCache.ValueRW.DownloadingFiles++;

                            if (status == FileStatus.Received)
                            {
                                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(data.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                                compilerCache.ValueRW.DownloadedFiles++;
                                return true;
                            }

                            sourcesFromOtherConnectionsNeeded = true;

                            if (status == FileStatus.Receiving)
                            {
                                return false;
                            }

                            FileChunkManager.TryGetFile(file, (data) =>
                            {
                                // Debug.Log($"Source \"{path}\" downloaded ...\n{Encoding.UTF8.GetString(data)}");
                            }, entityCommandBuffer);
                            compilerCache.ValueRW.CompileSecuedued = now + 5d;
                            // Debug.Log($"Source needs file \"{path}\" ...");

                            return false;
                        })
                    );
                    BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
                    {
                        StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
                    }, null, analysisCollection);

                    DynamicBuffer<BufferedInstruction> buffer = SystemAPI.GetBuffer<BufferedInstruction>(entity);

                    buffer.ResizeUninitialized(generated.Code.Length);
                    buffer.CopyFrom(generated.Code.Select(v => new BufferedInstruction(v)).ToArray());

                    compilerCache.ValueRW.CompileSecuedued = default;
                    compilerCache.ValueRW.Version = DateTime.UtcNow.Ticks; // compilerCache.ValueRO.SourceFile.Version;
                    compilerCache.ValueRW.IsSuccess = true;
                    // compilerCache.ValueRW.Version = File.GetLastWriteTimeUtc(compilerCache.ValueRO.SourceFile.ToString()).Ticks;
                    // compilerCache.ValueRW.HotReloadAt = Time.time + 5f;
                }
                catch (LanguageException exception)
                {
                    if (!sourcesFromOtherConnectionsNeeded)
                    {
                        Debug.LogWarning(exception);
                    }
                    analysisCollection.Errors.Add(new LanguageError(exception.Message, exception.Position, exception.File, false));
                }

                {
                    Entity request = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(request, new CompilerStatusRpc()
                    {
                        FileName = compilerCache.ValueRO.SourceFile,
                        DownloadingFiles = compilerCache.ValueRO.DownloadingFiles,
                        DownloadedFiles = compilerCache.ValueRO.DownloadedFiles,
                        IsSuccess = compilerCache.ValueRO.IsSuccess,
                        Version = compilerCache.ValueRO.Version,
                    });
                    entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
                    {
                        TargetConnection = compilerCache.ValueRO.SourceFile.Source.GetEntity(ref state),
                    });
                }

                DynamicBuffer<BufferedCompilationAnalystics> bufferedAnalyticsItems = SystemAPI.GetBuffer<BufferedCompilationAnalystics>(entity);

                bufferedAnalyticsItems.Clear();

                foreach (LanguageError item in analysisCollection.Errors)
                {
                    if (item.File is null) continue;
                    bufferedAnalyticsItems.Add(new BufferedCompilationAnalystics()
                    {
                        FileName = item.File.AbsolutePath,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Error,
                        Message = item.Message,
                    });
                    if (!item.File.TryGetNetcode(out FileId file)) continue;
                    Entity request = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
                    {
                        FileName = file,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Error,
                        Message = item.Message,
                    });
                    entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
                    {
                        TargetConnection = compilerCache.ValueRO.SourceFile.Source.GetEntity(ref state),
                    });
                    Debug.LogWarning(item);
                }

                foreach (Warning item in analysisCollection.Warnings)
                {
                    if (item.File is null) continue;
                    bufferedAnalyticsItems.Add(new BufferedCompilationAnalystics()
                    {
                        FileName = item.File.AbsolutePath,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Warning,
                        Message = item.Message,
                    });
                    if (!item.File.TryGetNetcode(out FileId file)) continue;
                    Entity request = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
                    {
                        FileName = file,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Warning,
                        Message = item.Message,
                    });
                    entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
                    {
                        TargetConnection = compilerCache.ValueRO.SourceFile.Source.GetEntity(ref state),
                    });
                    Debug.LogWarning(item);
                }

                foreach (Information item in analysisCollection.Informations)
                {
                    if (item.File is null) continue;
                    bufferedAnalyticsItems.Add(new BufferedCompilationAnalystics()
                    {
                        FileName = item.File.AbsolutePath,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Info,
                        Message = item.Message,
                    });
                    if (!item.File.TryGetNetcode(out FileId file)) continue;
                    Entity request = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
                    {
                        FileName = file,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Info,
                        Message = item.Message,
                    });
                    entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
                    {
                        TargetConnection = compilerCache.ValueRO.SourceFile.Source.GetEntity(ref state),
                    });
                    // Debug.LogWarning(item);
                }

                foreach (Hint item in analysisCollection.Hints)
                {
                    if (item.File is null) continue;
                    bufferedAnalyticsItems.Add(new BufferedCompilationAnalystics()
                    {
                        FileName = item.File.AbsolutePath,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Hint,
                        Message = item.Message,
                    });
                    if (!item.File.TryGetNetcode(out FileId file)) continue;
                    Entity request = entityCommandBuffer.CreateEntity();
                    entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
                    {
                        FileName = file,
                        Position = item.Position.Range.ToMutable(),

                        Type = CompilationAnalysticsItemType.Hint,
                        Message = item.Message,
                    });
                    entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
                    {
                        TargetConnection = compilerCache.ValueRO.SourceFile.Source.GetEntity(ref state),
                    });
                    // Debug.LogWarning(item);
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
