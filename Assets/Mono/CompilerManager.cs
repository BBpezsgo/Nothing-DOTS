using System;
using System.Collections.Frozen;
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
        (float)MonoTime.Now + 1f,
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
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.Label(rect, value.Status.ToString());
        GUI.enabled = t;
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

    public void Recompile(FileId file)
    {
        _compiledSources[file] = CompiledSource.Empty(file);
    }

    public void HandleRpc(CompilerStatusRpc rpc)
    {
        if (World.DefaultGameObjectInjectionWorld.IsServer()) return;
        _compiledSources[rpc.FileName] = CompiledSource.FromRpc(rpc);
    }

    static readonly FrozenDictionary<int, string> ExternalFunctionNames = new Dictionary<int, string>()
    {
        { 01, "stdout" },

        { 11, "sqrt" },
        { 12, "atan2" },
        { 13, "sin" },
        { 14, "cos" },
        { 15, "tan" },
        { 16, "asin" },
        { 17, "acos" },
        { 18, "atan" },

        { 21, "send" },
        { 22, "receive" },
        { 23, "radar" },

        { 31, "toglobal" },
        { 32, "tolocal" },
        { 33, "time" },
        { 34, "random" },

        { 41, "debug" },
        { 42, "ldebug" },
    }.ToFrozenDictionary();

    static IExternalFunction[]? _externalFunctions;
    public static unsafe IExternalFunction[] ExternalFunctions
    {
        get
        {
            if (_externalFunctions is not null) return _externalFunctions;

            ExternalFunctionScopedSync* scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[ProcessorSystemServer.ExternalFunctionCount];
            ProcessorSystemServer.GenerateExternalFunctions(scopedExternalFunctions);
            _externalFunctions = new IExternalFunction[ProcessorSystemServer.ExternalFunctionCount];

            for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
            {
                ref readonly ExternalFunctionScopedSync externalFunction = ref scopedExternalFunctions[i];
                _externalFunctions[i] = new ExternalFunctionSync(null!, externalFunction.Id, ExternalFunctionNames[externalFunction.Id], externalFunction.ParametersSize, externalFunction.ReturnValueSize);
            }

            return _externalFunctions;
        }
    }

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
                    if (source.CompileSecuedued > MonoTime.Now) continue;
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
            if (source.StatusChanged && source.LastStatusSync + 0.5f < Time.time)
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
            // Debug.Log($"Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        foreach (LanguageError item in source.AnalysisCollection.Errors.ToArray())
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

        foreach (Warning item in source.AnalysisCollection.Warnings.ToArray())
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
            // Debug.Log($"{item}\r\n{item.GetArrows()}");
        }

        foreach (Information item in source.AnalysisCollection.Informations.ToArray())
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

        foreach (Hint item in source.AnalysisCollection.Hints.ToArray())
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
        // Debug.Log($"Compilation started for \"{sourceUri}\" ...");

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

            (FileStatus status, _, _) = FileChunkManager.GetFileStatus(file, out RemoteFile data, true);

            if (status.IsOk())
            {
                parserResult = Parser.Parse(StringTokenizer.Tokenize(Encoding.UTF8.GetString(data.File.Data), PreprocessorVariables.Normal, uri, tokenizerSettings).Tokens, uri);
                return true;
            }

            if (status is FileStatus.NotFound or FileStatus.CachedNotFound)
            {
                return false;
            }

            sourcesFromOtherConnectionsNeeded = true;

            source.CompileSecuedued = MonoTime.Now + 5f;
            source.Status = CompilationStatus.Secuedued;
            source.StatusChanged = true;

            if (status == FileStatus.Receiving)
            {
                // Debug.Log($"Source \"{file}\" is downloading ...");
                return false;
            }

            ProgressRecord<(int, int)> progress = new(v =>
            {
                float total = progresses.Sum(v => (float)v.Progress.Item1 / (float)v.Progress.Item2);
                source.Progress = total / (float)progresses.Count;
                source.StatusChanged = true;
            });
            progresses.Add(progress);
            // Debug.Log($"Source needs file \"{file}\" ...");
            FileChunkManager.RequestFile(file, progress);
                // .GetAwaiter()
                // .OnCompleted(() =>
                // {
                //     Debug.Log($"Source \"{file}\" downloaded ...");
                // });

            return false;
        }

        try
        {
            // Debug.Log($"Compiling {file} ...");
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
            // Debug.Log($"Generating {file} ...");
            BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, new MainGeneratorSettings(MainGeneratorSettings.Default)
            {
                StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
            }, null, source.AnalysisCollection);

            // Debug.Log($"Done {file} ...");

            source.CompileSecuedued = default;
            source.Version = DateTime.UtcNow.Ticks;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code?.Dispose();
            source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
            source.Status = CompilationStatus.Compiled;
            source.StatusChanged = true;

            // Debug.Log($"Source {file} compiled");
        }
        catch (LanguageException exception)
        {
            if (!sourcesFromOtherConnectionsNeeded)
            { Instance._compiledSources[file].AnalysisCollection.Errors.Add(new LanguageError(exception.Message, exception.Position, exception.File, false)); }
        }

        if (sourcesFromOtherConnectionsNeeded)
        {
            Instance._compiledSources[file].AnalysisCollection.Clear();
            // Debug.Log($"Compilation will continue for \"{sourceUri}\" ...");
        }
        else
        {
            source.Status = CompilationStatus.Compiled;
            //  Debug.Log($"Compilation finished for \"{sourceUri}\"");
        }
    }
}
