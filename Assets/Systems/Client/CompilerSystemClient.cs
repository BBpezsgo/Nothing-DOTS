using System;
using System.Collections.Generic;
using System.Linq;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public class ClientSimpleDiagnostic : IDiagnostic
{
    public uint Id { get; }
    public DiagnosticsLevel Level { get; }
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }
    public List<ClientSimpleDiagnostic> SubErrors { get; }
    IEnumerable<IDiagnostic> IDiagnostic.SubErrors => SubErrors;

    public ClientSimpleDiagnostic(uint id, DiagnosticsLevel level, string message, Position position, Uri? file, List<ClientSimpleDiagnostic> suberrors)
    {
        Id = id;
        Level = level;
        Message = message;
        Position = position;
        File = file;
        SubErrors = suberrors;
    }
}

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
    IEnumerable<IDiagnostic> ICompiledSource.Diagnostics => ClientDiagnostics;
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

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial class CompilerSystemClient : SystemBase
{
    public readonly Dictionary<FileId, CompiledSourceClient> CompiledSources = new();

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach ((RefRO<CompilerStatusRpc> command, Entity entity) in
            SystemAPI.Query<RefRO<CompilerStatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            if (CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSourceClient? source))
            {
                source.Code?.Dispose();
                source.GeneratedFunction?.Dispose();
                source.UnitCommandDefinitions?.Dispose();

                source.Code = default;
                source.GeneratedFunction = default;
                source.UnitCommandDefinitions = default;
            }

            CompiledSources[command.ValueRO.FileName] = new(
                command.ValueRO.FileName,
                command.ValueRO.CompiledVersion,
                command.ValueRO.LatestVersion,
                command.ValueRO.Status,
                command.ValueRO.Progress,
                command.ValueRO.IsSuccess,
                new(command.ValueRO.UnitCommands, Allocator.Persistent)
            );
        }

        foreach ((RefRO<CompilerSubstatusRpc> command, Entity entity) in
            SystemAPI.Query<RefRO<CompilerSubstatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSourceClient source))
            {
                Debug.LogWarning(string.Format("[Client] Received substatus for unknown compiled source \"{0}\"", command.ValueRO.FileName));
                continue;
            }

            source.SubFiles.TryAdd(command.ValueRO.SubFileName, new ProgressRecord<(int Current, int Total)>(null));
            source.SubFiles[command.ValueRO.SubFileName].Report((command.ValueRO.CurrentProgress, command.ValueRO.TotalProgress));
        }

        foreach ((RefRO<UnitCommandDefinitionRpc> command, Entity entity) in
            SystemAPI.Query<RefRO<UnitCommandDefinitionRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSourceClient source))
            {
                Debug.LogWarning(string.Format("[Client] Received unit command for unknown compiled source \"{0}\"", command.ValueRO.FileName));
                continue;
            }

            if (!source.UnitCommandDefinitions.HasValue)
            {
                Debug.LogWarning(string.Format("[Client] Received unit command for compiled source \"{0}\" but the array is not created", command.ValueRO.FileName));
                continue;
            }

            source.UnitCommandDefinitions.Value.AsSpan()[command.ValueRO.Index] = new(
                command.ValueRO.Id,
                command.ValueRO.Label,
                command.ValueRO.Parameters
            );
        }

        foreach ((RefRO<CompilationAnalysticsRpc> command, Entity entity) in
            SystemAPI.Query<RefRO<CompilationAnalysticsRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.Source, out CompiledSourceClient source))
            {
                Debug.LogWarning(string.Format("[Client] Received diagnostics for unknown compiled source \"{0}\"", command.ValueRO.FileName));
                continue;
            }

            IReadOnlyList<CompilationAnalysticsRpc> TakeOrphanSubdiagnostics(uint parent)
            {
                List<CompilationAnalysticsRpc> result = new();
                for (int i = source.OrphanDiagnostics.Count - 1; i >= 0; i--)
                {
                    if (source.OrphanDiagnostics[i].Parent == parent)
                    {
                        source.OrphanDiagnostics.RemoveAt(i);
                        result.Add(source.OrphanDiagnostics[i]);
                    }
                }
                return result;
            }

            ClientSimpleDiagnostic ToDiagnostic(CompilationAnalysticsRpc diagnostic)
            {
                return new ClientSimpleDiagnostic(
                    diagnostic.Id,
                    command.ValueRO.Level,
                    command.ValueRO.Message.ToString(),
                    new Position(command.ValueRO.Position, command.ValueRO.AbsolutePosition),
                    command.ValueRO.FileName.ToUri(),
                    TakeOrphanSubdiagnostics(diagnostic.Id).Select(ToDiagnostic).ToList()
                );
            }

            if (command.ValueRO.Parent != 0)
            {
                for (int i = 0; i < source.ClientDiagnostics.Count; i++)
                {
                    ClientSimpleDiagnostic item = source.ClientDiagnostics[i];
                    if (item.Id == command.ValueRO.Parent)
                    {
                        item.SubErrors.Add(ToDiagnostic(command.ValueRO));
                        goto found;
                    }
                }
                source.OrphanDiagnostics.Add(command.ValueRO);
            found:;
            }
            else
            {
                source.ClientDiagnostics.Add(ToDiagnostic(command.ValueRO));
            }
        }
    }
}
