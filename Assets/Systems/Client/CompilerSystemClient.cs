using System.Net.WebSockets;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
partial struct CompilerSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilerStatusRpc>>()
            .WithEntityAccess())
        {
            // Debug.Log($"Received compilation status for {command.ValueRO.FileName}");

            if (!CompilerManager.Instance.CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
            {
                source = CompiledSource.FromRpc(command.ValueRO);
            }

            source.Version = command.ValueRO.Version;
            source.DownloadingFiles = command.ValueRO.DownloadingFiles;
            source.DownloadedFiles = command.ValueRO.DownloadedFiles;
            source.IsSuccess = command.ValueRO.IsSuccess;
            source.AnalysisCollection.Clear();

            CompilerManager.Instance.CompiledSources[source.SourceFile] = source;
            CompilerManager.Instance.CompileSecuedued = true;

            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilationAnalysticsRpc>>()
            .WithEntityAccess())
        {
            if (!CompilerManager.Instance.CompiledSources.TryGetValue(command.ValueRO.FileName, out CompiledSource source))
            {
                // Debug.LogWarning($"Received analytics for unknown compiled source \"{command.ValueRO.FileName}\"");
                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                entityCommandBuffer.DestroyEntity(entity);
                continue;
            }

            switch (command.ValueRO.Type)
            {
                case CompilationAnalysticsItemType.Error:
                    source.AnalysisCollection.Errors.Add(new LanguageCore.LanguageError(
                        command.ValueRO.Message.ToString(),
                        new LanguageCore.Position(command.ValueRO.Position, default),
                        command.ValueRO.FileName.ToUri(),
                        false
                    ));
                    break;
                case CompilationAnalysticsItemType.Warning:
                    source.AnalysisCollection.Warnings.Add(new LanguageCore.Warning(
                        command.ValueRO.Message.ToString(),
                        new LanguageCore.Position(command.ValueRO.Position, default),
                        command.ValueRO.FileName.ToUri()
                    ));
                    break;
                case CompilationAnalysticsItemType.Info:
                    source.AnalysisCollection.Informations.Add(new LanguageCore.Information(
                        command.ValueRO.Message.ToString(),
                        new LanguageCore.Position(command.ValueRO.Position, default),
                        command.ValueRO.FileName.ToUri()
                    ));
                    break;
                case CompilationAnalysticsItemType.Hint:
                    source.AnalysisCollection.Hints.Add(new LanguageCore.Hint(
                        command.ValueRO.Message.ToString(),
                        new LanguageCore.Position(command.ValueRO.Position, default),
                        command.ValueRO.FileName.ToUri()
                    ));
                    break;
            }

            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }
}
