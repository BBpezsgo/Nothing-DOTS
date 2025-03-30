using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial class CompilerSystemClient : SystemBase
{
    [NotNull] public readonly SerializableDictionary<FileId, CompiledSource>? CompiledSources = new();

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

            // Debug.Log($"Received compilation status for {command.ValueRO.FileName}");

            CompiledSources[command.ValueRO.FileName] = CompiledSource.FromRpc(command.ValueRO);
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<CompilerSubstatusRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            if (CompiledSources.TryGetValue(command.ValueRO.FileName, out var compiledSource))
            {
                compiledSource.SubFiles.TryAdd(command.ValueRO.SubFileName, new ProgressRecord<(int Current, int Total)>(null));
                compiledSource.SubFiles[command.ValueRO.SubFileName].Report((command.ValueRO.CurrentProgress, command.ValueRO.TotalProgress));
            }
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
                Debug.LogWarning($"Received analytics for unknown compiled source \"{command.ValueRO.FileName}\"");
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
