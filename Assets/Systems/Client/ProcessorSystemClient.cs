using System;
using System.Runtime.CompilerServices;
using LanguageCore;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
partial struct ProcessorSystemClient : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Processor>();
    }

    // [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = default;
        NativeHashSet<FileId> requestedSourceFiles = default;

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            Entity compilerCache = processor.ValueRO.CompilerCache;

            if (compilerCache == Entity.Null)
            {
                if (processor.ValueRO.SourceFile == default) continue;
                if (!requestedSourceFiles.IsCreated) requestedSourceFiles = new(8, AllocatorManager.Temp);
                if (!requestedSourceFiles.Add(processor.ValueRO.SourceFile)) continue;

                foreach (var (compilerCache_, compilerCacheEntity) in
                    SystemAPI.Query<RefRO<CompilerCache>>()
                    .WithEntityAccess())
                {
                    if (compilerCache_.ValueRO.SourceFile != processor.ValueRO.SourceFile) continue;

                    processor.ValueRW.CompilerCache = compilerCacheEntity;
                    compilerCache = compilerCacheEntity;

                    break;
                }

                if (compilerCache != Entity.Null) continue;

                if (!entityCommandBuffer.IsCreated) entityCommandBuffer = new(Allocator.Temp);
                compilerCache = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(compilerCache, new CompilerCache()
                {
                    SourceFile = processor.ValueRO.SourceFile,
                });
                entityCommandBuffer.AddBuffer<BufferedInstruction>(compilerCache);
                entityCommandBuffer.AddBuffer<BufferedCompilationAnalystics>(compilerCache);
                continue;
            }
        }

        if (entityCommandBuffer.IsCreated)
        {
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
        if (requestedSourceFiles.IsCreated)
        {
            requestedSourceFiles.Dispose();
        }
    }
}
