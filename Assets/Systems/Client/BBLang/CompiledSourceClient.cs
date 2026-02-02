using System.Collections.Generic;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Collections;

public class CompiledSourceClient : ICompiledSource
{
    public readonly FileId SourceFile;
    public long CompiledVersion;
    public long LatestVersion;
    public CompilationStatus Status;

    public float Progress;

    public bool IsSuccess;

    public NativeArray<Instruction>? Code;
    public NativeArray<ExternalFunctionScopedSync>? GeneratedFunction;
    public NativeArray<UnitCommandDefinition>? UnitCommandDefinitions;
    public List<ClientSimpleDiagnostic> ClientDiagnostics;
    public CompilerResult Compiled;
    public Dictionary<FileId, ProgressRecord<(int Current, int Total)>> SubFiles;
    public readonly List<CompilationAnalysticsRpc> OrphanDiagnostics = new();

    FileId ICompiledSource.SourceFile => SourceFile;
    CompilationStatus ICompiledSource.Status => Status;
    float ICompiledSource.Progress => Progress;
    bool ICompiledSource.IsSuccess => IsSuccess;
    IEnumerable<Diagnostic> ICompiledSource.Diagnostics => ClientDiagnostics;
    IReadOnlyDictionary<FileId, ProgressRecord<(int Current, int Total)>> ICompiledSource.SubFiles => SubFiles;

    public CompiledSourceClient(
        FileId sourceFile,
        long compiledVersion,
        long latestVersion,
        CompilationStatus status,
        float progress,
        bool isSuccess,
        NativeArray<UnitCommandDefinition>? unitCommandDefinitions)
    {
        SourceFile = sourceFile;
        CompiledVersion = compiledVersion;
        LatestVersion = latestVersion;
        Progress = progress;
        IsSuccess = isSuccess;
        UnitCommandDefinitions = unitCommandDefinitions;
        Status = status;
        Compiled = CompilerResult.MakeEmpty(sourceFile.ToUri());
        SubFiles = new();
        ClientDiagnostics = new();
    }
}
