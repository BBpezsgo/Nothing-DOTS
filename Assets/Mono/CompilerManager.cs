using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

public enum CompilationStatus
{
    None,
    Secuedued,
    Compiling,
    Compiled,
    Done,
}

public class CompiledSource : IInspect<CompiledSource>
{
    public readonly FileId SourceFile;
    public long Version;
    public CompilationStatus Status;

    public float CompileSecuedued;

    public float Progress;
    public bool StatusChanged;
    public float LastStatusSync;

    public bool IsSuccess;

    public NativeArray<Instruction>? Code;
    public CompiledDebugInformation DebugInformation;
    public AnalysisCollection AnalysisCollection;

    public CompiledSource(
        FileId sourceFile,
        long version,
        CompilationStatus status,
        float compileSecuedued,
        float progress,
        bool isSuccess,
        NativeArray<Instruction>? code,
        CompiledDebugInformation debugInformation,
        AnalysisCollection analysisCollection)
    {
        SourceFile = sourceFile;
        Version = version;
        CompileSecuedued = compileSecuedued;
        Progress = progress;
        IsSuccess = isSuccess;
        Code = code;
        DebugInformation = debugInformation;
        AnalysisCollection = analysisCollection;
        Status = status;
    }

    public static CompiledSource Empty(FileId sourceFile) => new(
        sourceFile,
        default,
        CompilationStatus.Secuedued,
        (float)DateTime.UtcNow.TimeOfDay.TotalSeconds + 1f,
        0,
        false,
        default,
        default,
        new AnalysisCollection()
    );

    public static CompiledSource FromRpc(CompilerStatusRpc rpc) => new(
        rpc.FileName,
        rpc.Version,
        CompilationStatus.None,
        default,
        rpc.Progress,
        rpc.IsSuccess,
        default,
        default,
        new AnalysisCollection()
    );

    public CompiledSource OnGUI(Rect rect, CompiledSource value)
    {
#if UNITY_EDITOR
        GUI.Label(rect, value.Status.ToString());
#endif
        return value;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(SerializableDictionary<FileId, CompiledSource>))]
public class _Drawer1 : DictionaryDrawer<FileId, CompiledSource> { }
#endif

public class CompilerManager : Singleton<CompilerManager>
{
    [SerializeField, NotNull] SerializableDictionary<FileId, CompiledSource>? _compiledSources = default;

    public IReadOnlyDictionary<FileId, CompiledSource> CompiledSources => _compiledSources;

    void Start()
    {
        _compiledSources = new();
    }

    public void CreateEmpty(FileId file)
    {
        _compiledSources.Add(file, CompiledSource.Empty(file));
    }

    public void HandleRpc(CompilerStatusRpc rpc)
    {
        if (World.DefaultGameObjectInjectionWorld.IsServer()) return;
        _compiledSources[rpc.FileName] = CompiledSource.FromRpc(rpc);
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
    };

    void FixedUpdate()
    {
        EntityCommandBuffer entityCommandBuffer = default;

        foreach ((FileId file, CompiledSource _source) in _compiledSources.ToArray())
        {
            CompiledSource source = _source;
            switch (source.Status)
            {
                case CompilationStatus.None:
                    break;
                case CompilationStatus.Secuedued:
                    if (source.CompileSecuedued > (float)DateTime.UtcNow.TimeOfDay.TotalSeconds) continue;
                    source.Status = CompilationStatus.Compiling;
                    source.CompileSecuedued = default;
                    Task.Factory.StartNew(static (object state)
                        => CompileSourceTask((FileId)state), (object)file);

                    if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                    SendCompilationStatus(source, entityCommandBuffer);
                    break;
                case CompilationStatus.Compiling:
                    break;
                case CompilationStatus.Compiled:
                    source.Status = CompilationStatus.Done;

                    if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                    SendCompilationStatus(source, entityCommandBuffer);
                    break;
                case CompilationStatus.Done:
                    break;
            }
            if (source.StatusChanged && source.LastStatusSync + 1f < Time.time)
            {
                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                SendCompilationStatus(source, entityCommandBuffer);
            }
            _compiledSources[file] = source;
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }

    public static void SendCompilationStatus(CompiledSource source, EntityCommandBuffer entityCommandBuffer)
    {
        source.LastStatusSync = Time.time;
        source.StatusChanged = false;
        {
            Entity request = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(request, new CompilerStatusRpc()
            {
                FileName = source.SourceFile,
                Status = source.Status,
                Progress = source.Progress,
                IsSuccess = source.IsSuccess,
                Version = source.Version,
            });
            entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            Debug.Log($"Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        foreach (LanguageError item in source.AnalysisCollection.Errors)
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

        foreach (Warning item in source.AnalysisCollection.Warnings)
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

        foreach (Information item in source.AnalysisCollection.Informations)
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

        foreach (Hint item in source.AnalysisCollection.Hints)
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

    public static void CompileSourceTask(FileId file)
    {
        Uri sourceUri = file.ToUri();
        Debug.Log($"Compilation started for \"{sourceUri}\" ...");

        bool sourcesFromOtherConnectionsNeeded = false;

        CompiledSource source = Instance.CompiledSources[file];

        source.Progress = 0;
        source.IsSuccess = false;
        source.AnalysisCollection = new AnalysisCollection();
        source.Status = CompilationStatus.Compiling;
        source.StatusChanged = true;

        List<ProgressRecord<(int, int)>> progresses = new();

        bool FileParser(Uri uri, TokenizerSettings? tokenizerSettings, out ParserResult parserResult)
        {
            parserResult = default;

            if (!uri.TryGetNetcode(out FileId file))
            { return false; }

            if (file.Source.IsServer)
            {
                FileData? localFile = FileChunkManager.GetLocalFile(file.Name.ToString());
                if (!localFile.HasValue)
                { return false; }

                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(localFile.Value.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                return true;
            }

            (FileStatus status, _, _) = FileChunkManager.GetFileStatus(file, out FileData data);

            if (status == FileStatus.Received)
            {
                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(data.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                return true;
            }

            sourcesFromOtherConnectionsNeeded = true;

            source.CompileSecuedued = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds + 5f;
            source.Status = CompilationStatus.Secuedued;
            source.StatusChanged = true;

            if (status == FileStatus.Receiving)
            {
                Debug.Log($"Source \"{file}\" is downloading ...");
                return false;
            }

            ProgressRecord<(int, int)> progress = new(v =>
            {
                float total = progresses.Sum(v => (float)v.Progress.Item1 / (float)v.Progress.Item2);
                source.Progress = total / (float)progresses.Count;
                source.StatusChanged = true;
            });
            progresses.Add(progress);
            Debug.Log($"Source needs file \"{file}\" ...");
            FileChunkManager.RequestFile(file, progress).GetAwaiter().OnCompleted(() =>
            {
                Debug.Log($"Source \"{file}\" downloaded ...");
            });

            return false;
        }

        try
        {
            Debug.Log($"Compiling {file} ...");
            CompilerResult compiled = Compiler.CompileFile(
                sourceUri,
                ExternalFunctions,
                new CompilerSettings()
                {
                    BasePath = null,
                },
                PreprocessorVariables.Normal,
                null,
                source.AnalysisCollection,
                null,
                FileParser
            );
            Debug.Log($"Generating {file} ...");
            BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
            {
                StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
            }, null, source.AnalysisCollection);

            Debug.Log($"Done {file} ...");

            source.CompileSecuedued = default;
            source.Version = DateTime.UtcNow.Ticks;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code?.Dispose();
            source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
            source.Status = CompilationStatus.Compiled;
            source.StatusChanged = true;

            Debug.Log($"Source {file} compiled");
        }
        catch (LanguageException exception)
        {
            if (!sourcesFromOtherConnectionsNeeded)
            { Instance._compiledSources[file].AnalysisCollection.Errors.Add(new LanguageError(exception.Message, exception.Position, exception.File, false)); }
        }

        if (sourcesFromOtherConnectionsNeeded)
        {
            Debug.Log($"Compilation will continue for \"{sourceUri}\" ...");
        }
        else
        {
            source.Status = CompilationStatus.Compiled;
            Debug.Log($"Compilation finished for \"{sourceUri}\"");
        }
    }
}
