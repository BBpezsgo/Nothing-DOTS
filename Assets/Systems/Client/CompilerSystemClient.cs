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
            foreach (var (compilerCache, compilerCacheEntity) in
                SystemAPI.Query<RefRW<CompilerCache>>()
                .WithEntityAccess())
            {
                if (compilerCache.ValueRO.SourceFile != command.ValueRO.FileName) continue;

                compilerCache.ValueRW.DownloadingFiles = command.ValueRO.DownloadingFiles;
                compilerCache.ValueRW.DownloadedFiles = command.ValueRO.DownloadedFiles;
                compilerCache.ValueRW.Version = command.ValueRO.Version;
                compilerCache.ValueRW.IsSuccess = command.ValueRO.IsSuccess;
                DynamicBuffer<BufferedCompilationAnalystics> compilationAnalytics = SystemAPI.GetBuffer<BufferedCompilationAnalystics>(compilerCacheEntity);
                compilationAnalytics.Clear();

                break;
            }

            if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
            entityCommandBuffer.DestroyEntity(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CompilationAnalysticsRpc>>()
            .WithEntityAccess())
        {
            foreach (var (compilerCache, compilerCacheEntity) in
                SystemAPI.Query<RefRO<CompilerCache>>()
                .WithEntityAccess())
            {
                // Debug.Log($"{compilerCache.ValueRO.SourceFile} ?= {command.ValueRO.FileName}");
                if (compilerCache.ValueRO.SourceFile != command.ValueRO.FileName) continue;

                DynamicBuffer<BufferedCompilationAnalystics> compilationAnalytics = SystemAPI.GetBuffer<BufferedCompilationAnalystics>(compilerCacheEntity);
                compilationAnalytics.Add(new BufferedCompilationAnalystics()
                {
                    FileName = command.ValueRO.FileName.Name,
                    Position = command.ValueRO.Position,

                    Type = command.ValueRO.Type,
                    Message = command.ValueRO.Message,
                });

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
