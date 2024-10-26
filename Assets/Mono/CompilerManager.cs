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
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using ReadOnlyAttribute = NaughtyAttributes.ReadOnlyAttribute;

public struct CompiledSource
{
    public readonly FileId SourceFile;
    public long Version;
    public float CompileSecuedued;
    public float HotReloadAt;
    public int DownloadingFiles;
    public int DownloadedFiles;
    public bool IsSuccess;
    public NativeArray<Instruction>? Code;
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
        NativeArray<Instruction>? code,
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
    [ReadOnly] public bool CompileSecuedued;

    void Start()
    {
        CompiledSources = new Dictionary<FileId, CompiledSource>();
    }

    public static readonly IExternalFunction[] ExternalFunctions = new IExternalFunction[]
    {
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 10, "atan2", ExternalFunctionGenerator.SizeOf<float, float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 11, "sin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 12, "cos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 13, "tan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 14, "asin", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 15, "acos", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 16, "atan", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
        new ExternalFunctionSync(static (ReadOnlySpan<byte> arguments, Span<byte> returnValue) =>
        { }, 17, "sqrt", ExternalFunctionGenerator.SizeOf<float>(), ExternalFunctionGenerator.SizeOf<float>()),
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

    void FixedUpdate()
    {
        EntityCommandBuffer entityCommandBuffer = default;

        KeyValuePair<FileId, CompiledSource> compiled = default;
        foreach ((FileId file, CompiledSource source) in CompiledSources)
        {
            if (source.CompileSecuedued == default ||
                source.CompileSecuedued > Time.time) continue;

            CompiledSource _source = source;
            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            CompileSource(ref _source, entityCommandBuffer);
            compiled = new KeyValuePair<FileId, CompiledSource>(file, _source);
            break;
        }

        if (compiled.Key != default)
        { CompiledSources[compiled.Key] = compiled.Value; }
        else
        { CompileSecuedued = false; }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(ConnectionManager.ServerOrDefaultWorld.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }

    public void CompileSource(ref CompiledSource source, EntityCommandBuffer entityCommandBuffer)
    {
        Uri sourceUri = source.SourceFile.ToUri();

        double now = Time.time;
        bool sourcesFromOtherConnectionsNeeded = false;
        AnalysisCollection analysisCollection = new();

        source.DownloadingFiles = 0;
        source.DownloadedFiles = 0;
        source.IsSuccess = false;

        CompiledSource _source = source;
        try
        {
            CompilerResult compiled = Compiler.CompileFile(
                sourceUri,
                ExternalFunctions,
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
                        Debug.Log($"Source \"{file}\" downloaded ...");
                    }, entityCommandBuffer);
                    _source.CompileSecuedued = Time.time + 5f;
                    Debug.Log($"Source needs file \"{file}\" ...");

                    return false;
                })
            );
            BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
            {
                StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
            }, null, analysisCollection);
            source = _source;

            source.CompileSecuedued = default;
            source.Version = DateTime.UtcNow.Ticks;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code?.Dispose();
            source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
            Debug.Log($"Source {source.SourceFile} compiled");
        }
        catch (LanguageException exception)
        {
            source = _source;
            if (!sourcesFromOtherConnectionsNeeded)
            { analysisCollection.Errors.Add(new LanguageError(exception.Message, exception.Position, exception.File, false)); }
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
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            Debug.Log($"Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
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
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            Debug.LogWarning($"{item}\r\n{item.GetArrows()}");
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
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            Debug.Log($"{item}\r\n{item.GetArrows()}");
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
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
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
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }
    }
}
