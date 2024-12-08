#pragma warning disable CS0162 // Unreachable code detected

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
    public long CompiledVersion;
    public long LatestVersion;
    public CompilationStatus Status;

    public float CompileSecuedued;

    public float Progress;
    public bool StatusChanged;
    public float LastStatusSync;

    public bool IsSuccess;

    public NativeArray<Instruction>? Code;
    public CompiledDebugInformation DebugInformation;
    public DiagnosticsCollection Diagnostics;
    public CompilerResult Compiled;

    CompiledSource(
        FileId sourceFile,
        long compiledVersion,
        long latestVersion,
        CompilationStatus status,
        float compileSecuedued,
        float progress,
        bool isSuccess,
        NativeArray<Instruction>? code,
        CompiledDebugInformation debugInformation,
        DiagnosticsCollection diagnostics)
    {
        SourceFile = sourceFile;
        CompiledVersion = compiledVersion;
        LatestVersion = latestVersion;
        CompileSecuedued = compileSecuedued;
        Progress = progress;
        IsSuccess = isSuccess;
        Code = code;
        DebugInformation = debugInformation;
        Diagnostics = diagnostics;
        Status = status;
        Compiled = CompilerResult.MakeEmpty(sourceFile.ToUri());
    }

    public static CompiledSource Empty(FileId sourceFile, long latestVersion) => new(
        sourceFile,
        default,
        latestVersion,
        CompilationStatus.Secuedued,
        (float)MonoTime.Now + 1f,
        0,
        false,
        default,
        default,
        new DiagnosticsCollection()
    );

    public static CompiledSource FromRpc(CompilerStatusRpc rpc) => new(
        rpc.FileName,
        rpc.CompiledVersion,
        rpc.LatestVersion,
        CompilationStatus.None,
        default,
        rpc.Progress,
        rpc.IsSuccess,
        default,
        default,
        new DiagnosticsCollection()
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
    const bool EnableLogging = false;

    [SerializeField, NotNull] SerializableDictionary<FileId, CompiledSource>? _compiledSources = default;

    public IReadOnlyDictionary<FileId, CompiledSource> CompiledSources => _compiledSources;

    public void AddEmpty(FileId file, long latestVersion)
    {
        _compiledSources.Add(file, CompiledSource.Empty(file, latestVersion));
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

        { 51, "dequeue_command" },
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

    void Start()
    {
        _compiledSources = new();
    }

    void FixedUpdate()
    {
        EntityCommandBuffer entityCommandBuffer = default;

        foreach ((FileId file, CompiledSource source) in _compiledSources)
        {
            switch (source.Status)
            {
                case CompilationStatus.None:
                    break;
                case CompilationStatus.Secuedued:
                    if (source.CompileSecuedued > MonoTime.Now) continue;
                    source.Status = CompilationStatus.Compiling;
                    source.CompileSecuedued = default;
                    Task.Factory.StartNew(static (object state)
                        // TODO: recompiling
                        => CompileSourceTask(((FileId, bool))state), (object)(file, false))
                        .ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            { Debug.LogException(task.Exception); }
                            else if (task.IsCanceled)
                            { Debug.LogError($"[{nameof(CompilerManager)}]: Compilation task cancelled"); }
                        });
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
                    if (source.CompiledVersion < source.LatestVersion)
                    {
                        Debug.Log($"[{nameof(CompilerManager)}]: Source version changed ({source.CompiledVersion} -> {source.LatestVersion}), recompiling \"{source.SourceFile}\"");
                        source.Status = CompilationStatus.Secuedued;
                        source.CompileSecuedued = 1f;
                        if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                        SendCompilationStatus(source, entityCommandBuffer);
                    }
                    break;
            }
            if (source.StatusChanged && source.LastStatusSync + 0.5f < Time.time)
            {
                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                SendCompilationStatus(source, entityCommandBuffer);
            }
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
                CompiledVersion = source.CompiledVersion,
                LatestVersion = source.LatestVersion,
            });
            entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            if (EnableLogging) Debug.Log($"[{nameof(CompilerManager)}]: Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        foreach (Diagnostic item in source.Diagnostics.Diagnostics.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerManager)}]: {item}\r\n{item.GetArrows()}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerManager)}]: {item}\r\n{item.GetArrows()}");

            if (item.File is null) continue;
            if (!item.File.TryGetNetcode(out FileId file)) continue;

            Entity request = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
            {
                FileName = file,
                Position = item.Position.Range.ToMutable(),
                AbsolutePosition = item.Position.AbsoluteRange.ToMutable(),
                Level = item.Level,
                Message = item.Message,
            });
            entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }

        foreach (DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerManager)}]: {item}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerManager)}]: {item}");

            Entity request = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
            {
                Level = item.Level,
                Message = item.Message,
            });
            entityCommandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }
    }

    public static unsafe void CompileSourceTask((FileId File, bool Force) args)
    {
        (FileId file, bool force) = args;

        Uri sourceUri = file.ToUri();
        if (EnableLogging) Debug.Log($"[{nameof(CompilerManager)}]: Compilation started for \"{sourceUri}\" ...");

        bool sourcesFromOtherConnectionsNeeded = false;

        CompiledSource source = Instance.CompiledSources[file];

        source.IsSuccess = false;
        source.Diagnostics = new DiagnosticsCollection();
        source.Status = CompilationStatus.Compiling;
        source.StatusChanged = true;

        List<ProgressRecord<(int, int)>> progresses = new();

        bool FileParser(Uri uri, [NotNullWhen(true)] out string? content)
        {
            content = default;

            if (!uri.TryGetNetcode(out FileId fileId))
            {
                Debug.LogError($"[{nameof(CompilerManager)}]: Uri \"{uri}\" aint a netcode uri");
                return false;
            }

            if (fileId.Source.IsServer)
            {
                FileData? localFile = FileChunkManager.GetLocalFile(fileId.Name.ToString());
                if (!localFile.HasValue)
                { return false; }

                content = Encoding.UTF8.GetString(localFile.Value.Data);
                return true;
            }

            if (FileChunkManager.TryGetRemoteFile(fileId, out RemoteFile remoteFile))
            {
                content = Encoding.UTF8.GetString(remoteFile.File.Data);
                return true;
            }

            FileStatus status = FileChunkManager.GetRequestStatus(fileId);

            if (status == FileStatus.NotFound)
            {
                Debug.LogError($"[{nameof(CompilerManager)}]: Remote file \"{uri}\" not found");
                return false;
            }

            sourcesFromOtherConnectionsNeeded = true;

            if (status == FileStatus.Receiving)
            {
                if (EnableLogging) Debug.Log($"[{nameof(CompilerManager)}]: Source \"{fileId}\" is downloading ...");
                return false;
            }

            ProgressRecord<(int, int)> progress = new(v =>
            {
                float total = progresses.Sum(v => v.Progress.Item2 == 0 ? 0f : (float)v.Progress.Item1 / (float)v.Progress.Item2);
                source.Progress = total / (float)progresses.Count;
                source.Diagnostics.Clear();
                source.StatusChanged = true;
            });
            progresses.Add(progress);
            if (EnableLogging) Debug.Log($"[{nameof(CompilerManager)}]: Source needs file \"{fileId}\" ...");

            Awaitable<RemoteFile> task = FileChunkManager.RequestFile(fileId, progress);
            task.GetAwaiter().OnCompleted(() =>
            {
                if (EnableLogging) Debug.Log($"[{nameof(CompilerManager)}]: Source \"{fileId}\" downloaded ...");
                if (source.Status == CompilationStatus.Secuedued &&
                    source.CompileSecuedued != 1f)
                {
                    source.CompileSecuedued = 1f;
                }
            });

            return false;
        }

        UserDefinedAttribute[] attributes = new UserDefinedAttribute[]
        {
            new("UnitCommand", new LiteralType[] { LiteralType.Integer, LiteralType.String }, CanUseOn.Struct, static (IHaveAttributes context, AttributeUsage attribute, [NotNullWhen(false)] out PossibleDiagnostic? error) =>
            {
                if (context is not CompiledStruct @struct)
                {
                    error = new PossibleDiagnostic($"This aint a struct");
                    return false;
                }

                error = null;
                return true;
            }),
            new("Context", new LiteralType[] { LiteralType.String }, CanUseOn.Field, static (IHaveAttributes context, AttributeUsage attribute, [NotNullWhen(false)] out PossibleDiagnostic? error) =>
            {
                if (context is not CompiledField field)
                {
                    error = new PossibleDiagnostic($"This aint a field");
                    return false;
                }

                if (!field.Context.Attributes.TryGetAttribute("UnitCommand", out _))
                {
                    error = new PossibleDiagnostic($"The struct should be flagged with [UnitCommand] attribute");
                    return false;
                }

                switch (attribute.Parameters[0].Value)
                {
                    case "position":
                    {
                        if (field.Type.GetSize(new CodeGeneratorForMain(CompilerResult.MakeEmpty(null!), MainGeneratorSettings.Default, new(), null)) != sizeof(float2))
                        {
                            error = new PossibleDiagnostic($"Fields with unit command context \"{attribute.Parameters[0].Value}\" should be a size of {sizeof(float2)} (a 2D float vector)");
                            return false;
                        }
                        break;
                    }
                    default:
                    {
                        error = new PossibleDiagnostic($"Unknown unit command context \"{attribute.Parameters[0].Value}\"", attribute.Parameters[0]);
                        return false;
                    }
                }

                error = null;
                return true;
            }),
        };

        CompilerResult compiled = CompilerResult.MakeEmpty(sourceUri);
        BBLangGeneratorResult generated = new()
        {
            Code = System.Collections.Immutable.ImmutableArray<Instruction>.Empty,
            DebugInfo = null,
        };
        try
        {
            compiled = Compiler.CompileFile(
                sourceUri,
                ExternalFunctions,
                new CompilerSettings()
                {
                    BasePath = null,
                },
                PreprocessorVariables.Normal,
                null,
                source.Diagnostics,
                null,
                FileParser,
                userDefinedAttributes: attributes
            );

            generated = CodeGeneratorForMain.Generate(
                compiled,
                new MainGeneratorSettings(MainGeneratorSettings.Default)
                {
                    StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
                },
                null,
                source.Diagnostics
            );
        }
        catch (LanguageException exception)
        {
            source.Diagnostics.Add(new Diagnostic(
                DiagnosticsLevel.Error,
                exception.Message,
                exception.Position,
                exception.File,
                null
            ));
        }
        catch (LanguageExceptionWithoutContext exception)
        {
            source.Diagnostics.Add(new DiagnosticWithoutContext(
                DiagnosticsLevel.Error,
                exception.Message
            ));
        }

        if (sourcesFromOtherConnectionsNeeded)
        {
            source.Diagnostics.Clear();
            source.CompileSecuedued = MonoTime.Now + 5f;
            source.Status = CompilationStatus.Secuedued;
            source.StatusChanged = true;
        }
        else if (source.Diagnostics.HasErrors)
        {
            source.Status = CompilationStatus.Compiled;
            source.Compiled = compiled;
            source.CompileSecuedued = default;
            source.CompiledVersion = DateTime.UtcNow.Ticks;
            source.IsSuccess = false;
            source.DebugInformation = new CompiledDebugInformation(null);
            source.Code?.Dispose();
            source.Code = default;
            source.Progress = float.NaN;
        }
        else
        {
            source.Status = CompilationStatus.Compiled;
            source.Compiled = compiled;
            source.CompileSecuedued = default;
            source.CompiledVersion = DateTime.UtcNow.Ticks;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code?.Dispose();
            source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
            source.Progress = float.NaN;
        }
    }
}
