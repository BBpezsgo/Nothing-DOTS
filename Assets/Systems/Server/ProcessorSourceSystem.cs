using System.Linq;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct ProcessorSourceSystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

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
                processor.ValueRW.SourceVersion = command.ValueRO.Version;

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach ((RefRW<Processor> processor, DynamicBuffer<BufferedUnitCommandDefinition> commandDefinitions, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>, DynamicBuffer<BufferedUnitCommandDefinition>>()
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
                CompilerManager.Instance.AddEmpty(processor.ValueRO.SourceFile);
                buffer.Clear();
                continue;
            }

            if (!source.Code.HasValue)
            {
                buffer.Clear();
                continue;
            }

            if (processor.ValueRO.SourceVersion < source.Version)
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

                commandDefinitions.Clear();

                foreach (CompiledStruct @struct in source.Compiled.Structs)
                {
                    if (!@struct.Attributes.TryGetAttribute("UnitCommand", out LanguageCore.Parser.AttributeUsage? structAttribute))
                    { continue; }

                    FixedList32Bytes<UnitCommandParameter> parameterTypes = new();
                    bool ok = true;

                    foreach (CompiledField field in @struct.Fields)
                    {
                        if (!field.Attributes.TryGetAttribute("Context", out LanguageCore.Parser.AttributeUsage? attribute)) continue;
                        switch (attribute.Parameters[0].Value)
                        {
                            case "position":
                                parameterTypes.Add(UnitCommandParameter.Position);
                                break;
                            default:
                                ok = false;
                                break;
                        }
                    }

                    if (!ok) continue;

                    commandDefinitions.Add(new(structAttribute.Parameters[0].GetInt(), structAttribute.Parameters[1].Value, parameterTypes));
                }

                buffer.Clear();
                NativeArray<BufferedInstruction> code = source.Code.Value.Reinterpret<BufferedInstruction>();
                buffer.CopyFrom(code);

                continue;
            }
            else if (processor.ValueRO.SourceVersion > source.Version)
            {
                CompilerManager.Instance.Recompile(processor.ValueRO.SourceFile);
                buffer.Clear();
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
