using System;
using System.Collections.Frozen;
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
    public CompilationStatus Status;

    public float CompileSecuedued;

    public float Progress;
    public bool StatusChanged;
    public double LastStatusSync;

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
        rpc.Status,
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

public partial class CompilerSystemServer : SystemBase
{
    static readonly bool EnableLogging = false;

    static readonly FrozenDictionary<int, string> ExternalFunctionNames = new Dictionary<int, string>()
    {
        { 01, "stdout" },
        { 02, "stdin" },

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

        { 43, "debug_label" },
        { 44, "ldebug_label" },

        { 51, "dequeue_command" },

        { 61, "gui_create" },
        { 62, "gui_destroy" },
        { 63, "gui_update" },
    }.ToFrozenDictionary();

    [NotNull] public readonly SerializableDictionary<FileId, CompiledSource>? CompiledSources = new();

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
                    if (source.CompileSecuedued > MonoTime.Now) continue;
                    source.Status = CompilationStatus.Compiling;
                    source.CompileSecuedued = default;
                    Task.Factory.StartNew(static (object state)
                        // TODO: recompiling
                        => CompileSourceTask(((FileId, bool, CompiledSource))state), (object)(file, false, CompiledSources[file]))
                        .ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            { Debug.LogException(task.Exception); }
                            else if (task.IsCanceled)
                            { Debug.LogError($"[{nameof(CompilerSystemServer)}]: Compilation task cancelled"); }
                        });
                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    SendCompilationStatus(source, commandBuffer);
                    break;
                case CompilationStatus.Compiling:
                    break;
                case CompilationStatus.Compiled:
                    source.Status = CompilationStatus.Done;

                    if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
                    SendCompilationStatus(source, commandBuffer);
                    break;
                case CompilationStatus.Done:
                    if (source.CompiledVersion < source.LatestVersion)
                    {
                        Debug.Log($"[{nameof(CompilerSystemServer)}]: Source version changed ({source.CompiledVersion} -> {source.LatestVersion}), recompiling \"{source.SourceFile}\"");
                        source.Status = CompilationStatus.Secuedued;
                        source.CompileSecuedued = 1f;
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

    void SendCompilationStatus(CompiledSource source, EntityCommandBuffer commandBuffer)
    {
        source.LastStatusSync = SystemAPI.Time.ElapsedTime;
        source.StatusChanged = false;
        {
            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(request, new CompilerStatusRpc()
            {
                FileName = source.SourceFile,
                Status = source.Status,
                Progress = source.Progress,
                IsSuccess = source.IsSuccess,
                CompiledVersion = source.CompiledVersion,
                LatestVersion = source.LatestVersion,
            });
            commandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
            if (EnableLogging) Debug.Log($"[{nameof(CompilerSystemServer)}]: Sending compilation status for {source.SourceFile} to {source.SourceFile.Source}");
        }

        // .ToArray() because the collection can be modified somewhere idk
        foreach (Diagnostic item in source.Diagnostics.Diagnostics.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}\r\n{item.GetArrows()}");

            if (item.File is null) continue;
            if (!item.File.TryGetNetcode(out FileId file)) continue;

            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(request, new CompilationAnalysticsRpc()
            {
                Source = source.SourceFile,
                FileName = file,
                Position = item.Position.Range.ToMutable(),
                AbsolutePosition = item.Position.AbsoluteRange.ToMutable(),
                Level = item.Level,
                Message = item.Message,
            });
            commandBuffer.AddComponent(request, new SendRpcCommandRequest()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }

        // .ToArray() because the collection can be modified somewhere idk
        foreach (DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext.ToArray())
        {
            if (item.Level == DiagnosticsLevel.Error) Debug.LogWarning($"[{nameof(CompilerSystemServer)}]: {item}");
            // if (item.Level == DiagnosticsLevel.Warning) Debug.Log($"[{nameof(CompilerSystemServer)}]: {item}");

            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<CompilationAnalysticsRpc>(request, new()
            {
                Source = source.SourceFile,
                Level = item.Level,
                Message = item.Message,
            });
            commandBuffer.AddComponent<SendRpcCommandRequest>(request, new()
            {
                TargetConnection = source.SourceFile.Source.GetEntity(),
            });
        }
    }

    public static unsafe void CompileSourceTask((FileId File, bool Force, CompiledSource source) args)
    {
        (FileId file, bool force, CompiledSource source) = args;

        Uri sourceUri = file.ToUri();
        if (EnableLogging) Debug.Log($"[{nameof(CompilerSystemServer)}]: Compilation started for \"{sourceUri}\" ...");

        bool sourcesFromOtherConnectionsNeeded = false;

        source.IsSuccess = false;
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
                    case "position":
                    {
                        if (field.Type.GetSize(new CodeGeneratorForMain(CompilerResult.MakeEmpty(null!), MainGeneratorSettings.Default, new())) != sizeof(float2))
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
            IExternalFunction[] externalFunctions = new IExternalFunction[ProcessorSystemServer.ExternalFunctionCount];
            unsafe
            {
                ExternalFunctionScopedSync* scopedExternalFunctions = stackalloc ExternalFunctionScopedSync[ProcessorSystemServer.ExternalFunctionCount];
                ProcessorSystemServer.GenerateExternalFunctions(scopedExternalFunctions);

                for (int i = 0; i < ProcessorSystemServer.ExternalFunctionCount; i++)
                {
                    ref readonly ExternalFunctionScopedSync externalFunction = ref scopedExternalFunctions[i];
                    externalFunctions[i] = externalFunction.ToManaged(ExternalFunctionNames[externalFunction.Id]);
                }
            }

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

    public void AddEmpty(FileId file, long latestVersion)
    {
        CompiledSources.Add(file, CompiledSource.Empty(file, latestVersion));
    }
}
