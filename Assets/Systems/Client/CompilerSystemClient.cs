using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial class CompilerSystemClient : SystemBase
{
    [NotNull] public readonly Dictionary<FileId, CompiledSource>? CompiledSources = new();

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilerStatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);
            if (CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource? source))
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
                default,
                command.ValueRO.Status,
                command.ValueRO.Progress,
                command.ValueRO.IsSuccess,
                default,
                new(command.ValueRO.UnitCommands, Allocator.Persistent),
                default,
                new()
            );
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilerSubstatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
            {
                Debug.LogWarning(string.Format("[Client] Received substatus for unknown compiled source \"{0}\"", command.ValueRO.FileName));
                continue;
            }

            source.SubFiles.TryAdd(command.ValueRO.SubFileName, new ProgressRecord<(int Current, int Total)>(null));
            source.SubFiles[command.ValueRO.SubFileName].Report((command.ValueRO.CurrentProgress, command.ValueRO.TotalProgress));
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<UnitCommandDefinitionRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
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

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilationAnalysticsRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (!CompiledSources.TryGetValue(command.ValueRO.Source, out CompiledSource source))
            {
                Debug.LogWarning(string.Format("[Client] Received diagnostics for unknown compiled source \"{0}\"", command.ValueRO.FileName));
                continue;
            }

            source.Diagnostics.Add(new LanguageCore.Diagnostic(
                command.ValueRO.Level,
                command.ValueRO.Message.ToString(),
                new LanguageCore.Position(command.ValueRO.Position, command.ValueRO.AbsolutePosition),
                command.ValueRO.FileName.ToUri(),
                null
            ));
        }
    }
}
