using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;
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
    public long HotReloadVersion;
    public CompilationStatus Status;

    public float Progress;
    public bool StatusChanged;
    public double LastStatusSync;

    public bool IsSuccess;

    public NativeArray<Instruction>? Code;
    public NativeArray<ExternalFunctionScopedSync>? GeneratedFunction;
    public CompiledDebugInformation DebugInformation;
    public DiagnosticsCollection Diagnostics;
    public CompilerResult Compiled;
    public BBLangGeneratorResult Generated;
    public Dictionary<FileId, ProgressRecord<(int Current, int Total)>> SubFiles;

    CompiledSource(
        FileId sourceFile,
        long compiledVersion,
        long latestVersion,
        long hotReloadVersion,
        CompilationStatus status,
        float progress,
        bool isSuccess,
        NativeArray<Instruction>? code,
        CompiledDebugInformation debugInformation,
        DiagnosticsCollection diagnostics)
    {
        SourceFile = sourceFile;
        CompiledVersion = compiledVersion;
        LatestVersion = latestVersion;
        HotReloadVersion = hotReloadVersion;
        Progress = progress;
        IsSuccess = isSuccess;
        Code = code;
        DebugInformation = debugInformation;
        Diagnostics = diagnostics;
        Status = status;
        Compiled = CompilerResult.MakeEmpty(sourceFile.ToUri());
        SubFiles = new();
    }

    public static CompiledSource Empty(FileId sourceFile, long latestVersion) => new(
        sourceFile,
        default,
        latestVersion,
        default,
        CompilationStatus.Secuedued,
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
        default,
        rpc.Status,
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

//#if UNITY_EDITOR
//[UnityEditor.CustomPropertyDrawer(typeof(SerializableDictionary<FileId, CompiledSource>))]
//public class _Drawer1 : DictionaryDrawer<FileId, CompiledSource> { }
//#endif

public partial class CompilerSystemServer : SystemBase
{
    static readonly bool EnableLogging = false;

    [NotNull] public readonly Dictionary<FileId, CompiledSource>? CompiledSources = new();

    readonly List<Task> _tasks = new();

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach ((FileId file, CompiledSource source) in CompiledSources)
        {
            switch (source.Status)
            {
                case CompilationStatus.None:
                    break;
                case CompilationStatus.Secuedued:
                    source.Status = CompilationStatus.Compiling;
                    _tasks.Add(Task.Factory.StartNew(static v => CompileSourceTask(((FileId, bool, CompiledSource))v), (file, false, CompiledSources[file]))
                        .ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            { Debug.LogException(task.Exception); }
                            else if (task.IsCanceled)
                            { Debug.LogError($"[{nameof(CompilerSystemServer)}]: Compilation task cancelled"); }
                        }));
                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    SendCompilationStatus(source, commandBuffer);
                    break;
                case CompilationStatus.Compiling:
                    break;
                case CompilationStatus.Compiled:
                    source.Status = CompilationStatus.Done;

                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    SendCompilationStatus(source, commandBuffer);
                    SendDiagnostics(source, commandBuffer);
                    break;
                case CompilationStatus.Done:
                    if (source.CompiledVersion < source.LatestVersion)
                    {
                        Debug.Log($"[Server] [{nameof(CompilerSystemServer)}] Source version changed ({source.CompiledVersion} -> {source.LatestVersion}), recompiling \"{source.SourceFile}\"");
                        source.Status = CompilationStatus.Secuedued;
                        if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                        SendCompilationStatus(source, commandBuffer);
                    }
                    break;
            }
            if (source.StatusChanged && source.LastStatusSync + 0.5d < SystemAPI.Time.ElapsedTime)
            {
                if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                SendCompilationStatus(source, commandBuffer);
            }
        }
    }

    protected override void OnDestroy()
    {
        if (_tasks.Count > 0)
        {
            Debug.Log($"Waiting for {_tasks.Count} compilation tasks to finish ...");
            Task.WaitAll(_tasks.ToArray());
            Debug.Log($"Compilation tasks finished");
        }
    }

    void SendCompilationStatus(CompiledSource source, EntityCommandBuffer commandBuffer)
    {
        source.LastStatusSync = SystemAPI.Time.ElapsedTime;
        source.StatusChanged = false;
        {
            Entity request = commandBuffer.CreateEntity(EntityManager.CreateArchetype(stackalloc ComponentType[]
            {
                ComponentType.ReadWrite<CompilerStatusRpc>(),
                ComponentType.ReadWrite<SendRpcCommandRequest>(),
            }));
            commandBuffer.SetComponent<CompilerStatusRpc>(request, new()
            {
                FileName = source.SourceFile,
                Status = source.Status,
                Progress = source.Progress,
                IsSuccess = source.IsSuccess,
                CompiledVersion = source.CompiledVersion,
                LatestVersion = source.LatestVersion,
            });
            commandBuffer.SetComponent<SendRpcCommandRequest>(request, new()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            if (EnableLogging) Debug.Log($"[Server] [{nameof(CompilerSystemServer)}] Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        EntityArchetype substatusRpcArchetype = EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            ComponentType.ReadWrite<CompilerSubstatusRpc>(),
            ComponentType.ReadWrite<SendRpcCommandRequest>(),
        });

        foreach (var subfile in source.SubFiles)
        {
            Entity request = commandBuffer.CreateEntity(substatusRpcArchetype);
            commandBuffer.SetComponent<CompilerSubstatusRpc>(request, new()
            {
                FileName = source.SourceFile,
                SubFileName = subfile.Key,
                CurrentProgress = subfile.Value.Progress.Current,
                TotalProgress = subfile.Value.Progress.Total,
            });
            commandBuffer.SetComponent<SendRpcCommandRequest>(request, new()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }
    }

    void SendDiagnostics(CompiledSource source, EntityCommandBuffer commandBuffer)
    {
        EntityArchetype analysticsRpcArchetype = EntityManager.CreateArchetype(stackalloc ComponentType[]
        {
            ComponentType.ReadWrite<CompilationAnalysticsRpc>(),
            ComponentType.ReadWrite<SendRpcCommandRequest>(),
        });

        // .ToArray() because the collection can be modified somewhere idk
        foreach (Diagnostic item in source.Diagnostics.Diagnostics.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");

            if (item.File is null) continue;
            if (!item.File.TryGetNetcode(out FileId file)) continue;

            Entity request = commandBuffer.CreateEntity(analysticsRpcArchetype);
            commandBuffer.SetComponent<CompilationAnalysticsRpc>(request, new()
            {
                Source = source.SourceFile,
                FileName = file,
                Position = item.Position.Range.ToMutable(),
                AbsolutePosition = item.Position.AbsoluteRange.ToMutable(),
                Level = item.Level,
                Message = item.Message,
            });
            commandBuffer.SetComponent<SendRpcCommandRequest>(request, new()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }

        // .ToArray() because the collection can be modified somewhere idk
        foreach (DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}");

            Entity request = commandBuffer.CreateEntity(analysticsRpcArchetype);
            commandBuffer.SetComponent<CompilationAnalysticsRpc>(request, new()
            {
                Source = source.SourceFile,
                Level = item.Level,
                Message = item.Message,
            });
            commandBuffer.SetComponent<SendRpcCommandRequest>(request, new()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }
    }

    public static unsafe void CompileSourceTask((FileId File, bool Force, CompiledSource source) args)
    {
        (FileId file, bool force, CompiledSource source) = args;

        Uri sourceUri = file.ToUri();
        if (EnableLogging) Debug.Log($"[Server] [{nameof(CompilerSystemServer)}] Compilation started for \"{sourceUri}\" ...");

        source.Diagnostics = new DiagnosticsCollection();
        source.Status = CompilationStatus.Compiling;
        source.StatusChanged = true;

        List<ProgressRecord<(int, int)>> progresses = new();

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
                    case "position2":
                    {
                        int size =field.Type.GetSize(new CodeGeneratorForMain(CompilerResult.MakeEmpty(null!), MainGeneratorSettings.Default, new()));
                        if (size != sizeof(float2))
                        {
                            error = new PossibleDiagnostic($"Fields with unit command context \"{attribute.Parameters[0].Value}\" should be a size of {sizeof(float2)} (a 2D float vector) (type {field.Type} has a size of {size} bytes)");
                            return false;
                        }
                        break;
                    }
                    case "position3":
                    {
                        int size =field.Type.GetSize(new CodeGeneratorForMain(CompilerResult.MakeEmpty(null!), MainGeneratorSettings.Default, new()));
                        if (size != sizeof(float3))
                        {
                            error = new PossibleDiagnostic($"Fields with unit command context \"{attribute.Parameters[0].Value}\" should be a size of {sizeof(float3)} (a 3D float vector) (type {field.Type} has a size of {size} bytes)");
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
            Code = ImmutableArray<Instruction>.Empty,
            DebugInfo = null,
        };
        try
        {
            IExternalFunction[] externalFunctions = ProcessorAPI.GenerateManagedExternalFunctions();

            Debug.Log($"Compiling {sourceUri} ...");

            compiled = StatementCompiler.CompileFile(
                sourceUri.ToString(),
                new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
                {
                    UserDefinedAttributes = attributes,
                    ExternalFunctions = externalFunctions.ToImmutableArray(),
                    DontOptimize = false,
                    SourceProviders = ImmutableArray.Create<ISourceProvider>(
                        new NetcodeSourceProvider(source, progresses, EnableLogging)
                    ),
                },
                source.Diagnostics
            );

            Debug.Log($"Generating {sourceUri} ...");

            generated = CodeGeneratorForMain.Generate(
                compiled,
                new MainGeneratorSettings(MainGeneratorSettings.Default)
                {
                    StackSize = ProcessorSystemServer.BytecodeInterpreterSettings.StackSize,
                    ILGeneratorSettings = new LanguageCore.IL.Generator.ILGeneratorSettings()
                    {
                        AllowCrash = false,
                        AllowHeap = false,
                        AllowPointers = true,
                    },
                },
                null,
                source.Diagnostics
            );

            Debug.Log($"{sourceUri} done");
        }
        catch (LanguageException exception)
        {
            source.IsSuccess = false;
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
            source.IsSuccess = false;
            source.Diagnostics.Add(new DiagnosticWithoutContext(
                DiagnosticsLevel.Error,
                exception.Message
            ));
        }

        if (source.Diagnostics.HasErrors)
        {
            source.Status = CompilationStatus.Compiled;
            source.Compiled = compiled;
            source.Generated = generated;
            Debug.Log($"Updating source version ({source.CompiledVersion} -> {source.LatestVersion})");
            source.CompiledVersion = source.LatestVersion;
            source.IsSuccess = false;
            source.DebugInformation = new CompiledDebugInformation(null);
            source.Code?.Dispose();
            source.Code = default;
            source.GeneratedFunction?.Dispose();
            source.GeneratedFunction = default;
            source.Progress = float.NaN;
        }
        else
        {
            source.Status = CompilationStatus.Compiled;
            source.Compiled = compiled;
            source.Generated = generated;
            Debug.Log($"Updating source version ({source.CompiledVersion} -> {source.LatestVersion})");
            source.CompiledVersion = source.LatestVersion;
            source.IsSuccess = true;
            source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
            source.Code?.Dispose();
            source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
            source.GeneratedFunction?.Dispose();
            source.GeneratedFunction = new NativeArray<ExternalFunctionScopedSync>(generated.GeneratedUnmanagedFunctions.ToArray(), Allocator.Persistent);
            source.Progress = float.NaN;
        }
    }

    public void AddEmpty(FileId file, long latestVersion)
    {
        CompiledSources.Add(file, CompiledSource.Empty(file, latestVersion));
    }
}
