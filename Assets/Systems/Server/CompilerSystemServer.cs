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
using Unity.Profiling;
using UnityEngine;

public enum CompilationStatus
{
    None,
    Secuedued,
    Uploading,
    Compiling,
    Generating,
    Generated,
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
    public NativeArray<UnitCommandDefinition>? UnitCommandDefinitions;
    public CompiledDebugInformation DebugInformation;
    public DiagnosticsCollection Diagnostics;
    public CompilerResult Compiled;
    public BBLangGeneratorResult Generated;
    public Dictionary<FileId, ProgressRecord<(int Current, int Total)>> SubFiles;

    public CompiledSource(
        FileId sourceFile,
        long compiledVersion,
        long latestVersion,
        long hotReloadVersion,
        CompilationStatus status,
        float progress,
        bool isSuccess,
        NativeArray<Instruction>? code,
        NativeArray<UnitCommandDefinition>? unitCommandDefinitions,
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
        UnitCommandDefinitions = unitCommandDefinitions;
        DebugInformation = debugInformation;
        Diagnostics = diagnostics;
        Status = status;
        Compiled = CompilerResult.MakeEmpty(sourceFile.ToUri());
        SubFiles = new();
    }

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
            lock (source)
            {
                switch (source.Status)
                {
                    case CompilationStatus.None:
                        break;
                    case CompilationStatus.Secuedued:
                        source.Status = CompilationStatus.Compiling;
                        _tasks.Add(Task.Factory.StartNew(static v => CompileSourceTask(((FileId, bool, CompiledSource))v), (file, false, source))
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
                    case CompilationStatus.Uploading:
                    case CompilationStatus.Generating:
                        break;
                    case CompilationStatus.Generated:
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
                    default:
                        throw new UnreachableException();
                }
                if (source.StatusChanged && source.LastStatusSync + 0.5d < SystemAPI.Time.ElapsedTime)
                {
                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    SendCompilationStatus(source, commandBuffer);
                }
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
            NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CompilerStatusRpc()
            {
                FileName = source.SourceFile,
                Status = source.Status,
                Progress = source.Progress,
                IsSuccess = source.IsSuccess,
                CompiledVersion = source.CompiledVersion,
                LatestVersion = source.LatestVersion,
                UnitCommands = source.UnitCommandDefinitions?.Length ?? 0,
            }, source.SourceFile.Source.GetEntity(World.EntityManager));
            if (EnableLogging) Debug.Log($"[Server] [{nameof(CompilerSystemServer)}] Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        if (source.IsSuccess && source.UnitCommandDefinitions.HasValue)
        {
            FixedList64Bytes<UnitCommandParameter> parameters = new();

            for (int i = 0; i < source.UnitCommandDefinitions.Value.Length; i++)
            {
                UnitCommandDefinition item = source.UnitCommandDefinitions.Value[i];
                unsafe
                {
                    parameters.Clear();
                    parameters.AddRange(&item.Parameters, item.ParameterCount);
                }
                NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new UnitCommandDefinitionRpc()
                {
                    FileName = source.SourceFile,
                    Index = i,
                    Id = item.Id,
                    Label = item.Label,
                    Parameters = parameters,
                }, source.SourceFile.Source.GetEntity(EntityManager));
            }
        }

        foreach (var subfile in source.SubFiles)
        {
            NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CompilerSubstatusRpc()
            {
                FileName = source.SourceFile,
                SubFileName = subfile.Key,
                CurrentProgress = subfile.Value.Progress.Current,
                TotalProgress = subfile.Value.Progress.Total,
            }, source.SourceFile.Source.GetEntity());
        }
    }

    void SendDiagnostics(CompiledSource source, EntityCommandBuffer commandBuffer)
    {
        // .ToArray() because the collection can be modified somewhere idk
        foreach (Diagnostic item in source.Diagnostics.Diagnostics.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");

            if (item.File is null) continue;
            if (!item.File.TryGetNetcode(out FileId file)) continue;

            NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CompilationAnalysticsRpc()
            {
                Source = source.SourceFile,
                FileName = file,
                Position = item.Position.Range.ToMutable(),
                AbsolutePosition = item.Position.AbsoluteRange.ToMutable(),
                Level = item.Level,
                Message = item.Message,
            }, source.SourceFile.Source.GetEntity());
        }

        // .ToArray() because the collection can be modified somewhere idk
        foreach (DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}");

            NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new CompilationAnalysticsRpc()
            {
                Source = source.SourceFile,
                Level = item.Level,
                Message = item.Message,
            }, source.SourceFile.Source.GetEntity());
        }
    }

    static readonly ProfilerMarker _markerCompiler = new("Compiler");
    static readonly ProfilerMarker _markerCompilerCompilation = new("Compiler.Compilation");
    static readonly ProfilerMarker _markerCompilerGeneration = new("Compiler.Generation");

    public static unsafe void CompileSourceTask((FileId File, bool Force, CompiledSource source) args)
    {
        using ProfilerMarker.AutoScope _m = _markerCompiler.Auto();

        (FileId file, bool force, CompiledSource source) = args;

        Uri sourceUri = file.ToUri();
        if (EnableLogging) Debug.Log($"[Server] [{nameof(CompilerSystemServer)}] Compilation started for \"{sourceUri}\" ...");

        lock (source)
        {
            source.Diagnostics = new DiagnosticsCollection();
            source.Status = CompilationStatus.Uploading;
            source.StatusChanged = true;
        }

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

            lock (source)
            {
                source.Status = CompilationStatus.Uploading;
                source.StatusChanged = true;
            }

            using (ProfilerMarker.AutoScope _2 = _markerCompilerCompilation.Auto())
            {
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
            }

            lock (source)
            {
                source.Status = CompilationStatus.Generating;
                source.StatusChanged = true;
            }

            Debug.Log($"Generating {sourceUri} ...");

            using (ProfilerMarker.AutoScope _2 = _markerCompilerGeneration.Auto())
            {
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
            }

            //using (StreamWriter stringWriter = new($"/home/bb/Projects/BBLang/Core/out-{DateTime.Now:O}-{file.Name.ToString().Replace('/', '_')}.bbc"))
            //{
            //    stringWriter.WriteLine(compiled.Stringify());
            //    if (!generated.ILGeneratorBuilders.IsDefault)
            //    {
            //        foreach (string builder in generated.ILGeneratorBuilders)
            //        {
            //            stringWriter.WriteLine(builder);
            //        }
            //    }
            //}

            Debug.Log($"{sourceUri} done");
        }
        catch (LanguageException exception)
        {
            lock (source)
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
        }
        catch (LanguageExceptionWithoutContext exception)
        {
            lock (source)
            {
                source.IsSuccess = false;
                source.Diagnostics.Add(new DiagnosticWithoutContext(
                    DiagnosticsLevel.Error,
                    exception.Message
                ));
            }
        }

        if (source.Diagnostics.HasErrors)
        {
            lock (source)
            {
                source.Status = CompilationStatus.Generated;
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
                source.UnitCommandDefinitions?.Dispose();
                source.UnitCommandDefinitions = default;

                source.Progress = float.NaN;
            }
        }
        else
        {
            lock (source)
            {
                source.Compiled = compiled;
                source.Generated = generated;
                source.DebugInformation = new CompiledDebugInformation(generated.DebugInfo);
                source.Code?.Dispose();
                source.Code = new NativeArray<Instruction>(generated.Code.ToArray(), Allocator.Persistent);
                source.GeneratedFunction?.Dispose();
                source.GeneratedFunction = new NativeArray<ExternalFunctionScopedSync>(generated.GeneratedUnmanagedFunctions.ToArray(), Allocator.Persistent);
                source.UnitCommandDefinitions?.Dispose();
                List<UnitCommandDefinition> commandDefinitions = new();
                foreach (CompiledStruct @struct in source.Compiled.Structs)
                {
                    if (!@struct.Attributes.TryGetAttribute("UnitCommand", out AttributeUsage? structAttribute))
                    { continue; }

                    FixedList32Bytes<UnitCommandParameter> parameterTypes = new();
                    bool ok = true;

                    foreach (CompiledField field in @struct.Fields)
                    {
                        if (!field.Attributes.TryGetAttribute("Context", out AttributeUsage? attribute)) continue;
                        switch (attribute.Parameters[0].Value)
                        {
                            case "position2":
                                parameterTypes.Add(UnitCommandParameter.Position2);
                                break;
                            case "position3":
                                parameterTypes.Add(UnitCommandParameter.Position3);
                                break;
                            default:
                                ok = false;
                                break;
                        }
                    }

                    if (!ok) continue;

                    commandDefinitions.Add(new(structAttribute.Parameters[0].GetInt(), structAttribute.Parameters[1].Value, parameterTypes));
                }
                source.UnitCommandDefinitions = new(commandDefinitions.ToArray(), Allocator.Persistent);

                source.Status = CompilationStatus.Generated;
                Debug.Log($"Updating source version ({source.CompiledVersion} -> {source.LatestVersion})");
                source.CompiledVersion = source.LatestVersion;
                source.IsSuccess = true;
                source.Progress = float.NaN;
            }
        }
    }

    public void AddEmpty(FileId file, long latestVersion) => CompiledSources.Add(file, new(
        file,
        default,
        latestVersion,
        default,
        CompilationStatus.Secuedued,
        0,
        false,
        default,
        default,
        default,
        new DiagnosticsCollection()
    ));
}
