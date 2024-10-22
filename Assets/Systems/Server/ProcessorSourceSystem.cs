using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct ProcessorSourceSystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SetProcessorSourceRequestRpc>>()
            .WithEntityAccess())
        {
            foreach (var (ghostInstance, ghostEntity) in
                SystemAPI.Query<RefRO<GhostInstance>>()
                .WithEntityAccess())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                RefRW<Processor> processor = SystemAPI.GetComponentRW<Processor>(ghostEntity);
                processor.ValueRW.CompilerCache = Entity.Null;
                processor.ValueRW.SourceFile = new FileId(command.ValueRO.Source, request.ValueRO.SourceConnection);
                processor.ValueRW.SourceVersion = default;

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
