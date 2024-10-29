using System.Linq;
using LanguageCore.Runtime;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct ProcessorSourceSystem : ISystem
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
                NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);

                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                RefRW<Processor> processor = SystemAPI.GetComponentRW<Processor>(ghostEntity);
                processor.ValueRW.SourceFile = new FileId(command.ValueRO.Source, ep);
                processor.ValueRW.SourceVersion = default;

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach ((RefRW<Processor> processor, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>>()
                    .WithEntityAccess())
        {
            DynamicBuffer<BufferedInstruction> buffer = SystemAPI.GetBuffer<BufferedInstruction>(entity);

            if (processor.ValueRO.SourceFile == default)
            {
                buffer.Clear();
                continue;
            }

            if (!CompilerManager.Instance.CompiledSources.TryGetValue(processor.ValueRO.SourceFile, out var source))
            {
                // Debug.Log($"Request source \"{processor.ValueRO.SourceFile}\" ...");
                CompilerManager.Instance.CreateEmpty(processor.ValueRO.SourceFile);
                buffer.Clear();
                continue;
            }

            if (!source.Code.HasValue)
            {
                buffer.Clear();
                continue;
            }

            if (processor.ValueRO.SourceVersion != source.Version)
            {
                processor.ValueRW.StdOutBuffer.Clear();

                ProcessorState processorState_ = new(
                    ProcessorSystemServer.BytecodeInterpreterSettings,
                    default,
                    default,
                    default,
                    default,
                    default,
                    default
                );
                processorState_.Setup();
                processor.ValueRW.Registers = processorState_.Registers;
                processor.ValueRW.SourceVersion = source.Version;

                buffer.Clear();
                BufferedInstruction[] code = source.Code.Value.Select(v => new BufferedInstruction(v)).ToArray();
                buffer.CopyFrom(code);

                continue;
            }

            if (!source.IsSuccess)
            {
                buffer.Clear();
            }
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
