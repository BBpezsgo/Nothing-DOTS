#pragma warning disable CS0162 // Unreachable code detected

using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
unsafe partial struct ProcessorSourceSystem : ISystem
{
    const bool EnableLogging = false;

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var compilerSystem = state.World.GetExistingSystemManaged<CompilerSystemServer>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ProcessorCommandRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            foreach (var (ghostInstance, processor) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<Processor>>())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                switch (command.ValueRO.Command)
                {
                    case ProcessorCommand.Halt:
                        processor.ValueRW.Signal = Signal.Halt;
                        break;
                    case ProcessorCommand.Reset:
                        ResetProcessor(processor);
                        break;
                    case ProcessorCommand.Continue:
                        processor.ValueRW.Signal = Signal.None;
                        processor.ValueRW.Crash = 0;
                        break;
                    case ProcessorCommand.Key:
                        processor.ValueRW.InputKey = unchecked((char)command.ValueRO.Data);
                        break;
                }

                break;
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SetProcessorSourceRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            foreach (var (ghostInstance, processor) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<Processor>>())
            {
                NetcodeEndPoint ep = new(SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO, request.ValueRO.SourceConnection);
                if (!state.World.IsServer()) ep = NetcodeEndPoint.Server;

                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                processor.ValueRW.SourceFile = new FileId(command.ValueRO.Source, ep);

                if (compilerSystem.CompiledSources.TryGetValue(processor.ValueRO.SourceFile, out CompiledSource? source))
                {
                    if (EnableLogging)
                    {
                        if (source.LatestVersion == command.ValueRO.Version)
                        { Debug.Log(string.Format("Source file {0} not changed", command.ValueRO.Source)); }
                        else
                        { Debug.Log(string.Format("Update source file {0} latest version ({1} -> {2})", command.ValueRO.Source, source.LatestVersion, command.ValueRO.Version)); }
                    }
                    source.LatestVersion = command.ValueRO.Version;
                }
                else
                {
                    if (EnableLogging) Debug.Log(string.Format("Creating new source file {0}", command.ValueRO.Source));
                    compilerSystem.AddEmpty(processor.ValueRO.SourceFile, command.ValueRO.Version);
                }

                break;
            }
        }

        foreach (var (processor, commandDefinitions, instructions) in
                    SystemAPI.Query<RefRW<Processor>, DynamicBuffer<BufferedUnitCommandDefinition>, DynamicBuffer<BufferedInstruction>>())
        {
            if (processor.ValueRO.SourceFile == default)
            {
                instructions.Clear();
                continue;
            }

            if (!compilerSystem.CompiledSources.TryGetValue(processor.ValueRO.SourceFile, out CompiledSource? source))
            {
                if (EnableLogging) Debug.Log(string.Format("Creating new source file {0} (internal)", processor.ValueRO.SourceFile));
                compilerSystem.AddEmpty(processor.ValueRO.SourceFile, default);
                instructions.Clear();
                continue;
            }

            if (!source.Code.HasValue)
            {
                instructions.Clear();
                continue;
            }

            if (processor.ValueRO.CompiledSourceVersion != source.CompiledVersion)
            {
                ResetProcessor(processor);
                processor.ValueRW.CompiledSourceVersion = source.CompiledVersion;

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

                instructions.Clear();
                NativeArray<BufferedInstruction> code = source.Code.Value.Reinterpret<BufferedInstruction>();
                instructions.CopyFrom(code);

                continue;
            }

            if (!source.IsSuccess)
            {
                instructions.Clear();
            }
        }
    }

    public static void ResetProcessor(RefRW<Processor> processor)
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
        processor.ValueRW.Signal = processorState_.Signal;
        processor.ValueRW.Crash = processorState_.Crash;
    }
}
