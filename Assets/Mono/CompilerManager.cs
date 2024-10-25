using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using Maths;
using NaughtyAttributes;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct CompiledSource
{
    public readonly FileId SourceFile;
    public long Version;
    public float CompileSecuedued;
    public float HotReloadAt;
    public int DownloadingFiles;
    public int DownloadedFiles;
    public bool IsSuccess;
    public ImmutableArray<Instruction>? Code;
    public CompiledDebugInformation DebugInformation;
    public AnalysisCollection AnalysisCollection;

    public CompiledSource(
        FileId sourceFile,
        long version,
        float compileSecuedued,
        float hotReloadAt,
        int downloadingFiles,
        int downloadedFiles,
        bool isSuccess,
        ImmutableArray<Instruction>? code,
        CompiledDebugInformation debugInformation,
        AnalysisCollection analysisCollection)
    {
        SourceFile = sourceFile;
        Version = version;
        CompileSecuedued = compileSecuedued;
        HotReloadAt = hotReloadAt;
        DownloadingFiles = downloadingFiles;
        DownloadedFiles = downloadedFiles;
        IsSuccess = isSuccess;
        Code = code;
        DebugInformation = debugInformation;
        AnalysisCollection = analysisCollection;
    }

    public static CompiledSource Empty(FileId sourceFile) => new(
        sourceFile,
        default,
        Time.time + 1f,
        0f,
        0,
        0,
        false,
        default,
        default,
        new AnalysisCollection()
    );

    public static CompiledSource FromRpc(CompilerStatusRpc rpc) => new(
        rpc.FileName,
        rpc.Version,
        default,
        default,
        rpc.DownloadingFiles,
        rpc.DownloadedFiles,
        rpc.IsSuccess,
        default,
        default,
        new AnalysisCollection()
    );
}

public class CompilerManager : Singleton<CompilerManager>
{
    [ReadOnly, NotNull, NonReorderable] public Dictionary<FileId, CompiledSource>? CompiledSources = default;

    void Start()
    {
        CompiledSources = new Dictionary<FileId, CompiledSource>();
    }

    public void CompileSource(ref CompiledSource source, EntityCommandBuffer entityCommandBuffer, ref SystemState systemState)
    {
        Uri sourceUri = source.SourceFile.ToUri();

        double now = Time.time;
        bool sourcesFromOtherConnectionsNeeded = false;
        AnalysisCollection analysisCollection = new();

        source.DownloadingFiles = 0;
        source.DownloadedFiles = 0;
        source.IsSuccess = false;

        try
        {
            CompiledSource _source = source;
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

                    _source.DownloadingFiles++;

                    if (status == FileStatus.Received)
                    {
                        parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(data.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                        _source.DownloadedFiles++;
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
                    _source.CompileSecuedued = Time.time + 5f;
                    // Debug.Log($"Source needs file \"{path}\" ...");

                    return false;
                })
            );
            BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
            {
                StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
            }, null, analysisCollection);
            source = _source;

            source.CompileSecuedued = default;
            source.Version = DateTime.UtcNow.Ticks; // source.SourceFile.Version;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code = generated.Code;
            // Debug.Log($"Source {source.SourceFile} compiled");
            // source.Version = File.GetLastWriteTimeUtc(source.SourceFile.ToString()).Ticks;
            // source.HotReloadAt = Time.time + 5f;
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
                FileName = source.SourceFile,
                DownloadingFiles = source.DownloadingFiles,
                DownloadedFiles = source.DownloadedFiles,
                IsSuccess = source.IsSuccess,
                Version = source.Version,
            });
            entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(ref systemState),
            });
            // Debug.Log($"Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        foreach (LanguageError item in analysisCollection.Errors)
        {
            if (item.File is null) continue;
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
                TargetConnection = source.SourceFile.Source.GetEntity(ref systemState),
            });
            Debug.LogWarning(item);
        }

        foreach (Warning item in analysisCollection.Warnings)
        {
            if (item.File is null) continue;
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
                TargetConnection = source.SourceFile.Source.GetEntity(ref systemState),
            });
            Debug.LogWarning(item);
        }

        foreach (Information item in analysisCollection.Informations)
        {
            if (item.File is null) continue;
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
                TargetConnection = source.SourceFile.Source.GetEntity(ref systemState),
            });
            // Debug.LogWarning(item);
        }

        foreach (Hint item in analysisCollection.Hints)
        {
            if (item.File is null) continue;
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
                TargetConnection = source.SourceFile.Source.GetEntity(ref systemState),
            });
            // Debug.LogWarning(item);
        }
    }
}
