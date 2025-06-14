using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct UnitCommandReceiver : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<UnitCommandRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            RefRO<NetworkId> requestConnection = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            int sourceTeam = -1;
            foreach (var player in
                SystemAPI.Query<RefRO<Player>>())
            {
                if (player.ValueRO.ConnectionId != requestConnection.ValueRO.Value) continue;
                sourceTeam = player.ValueRO.Team;
                break;
            }

            if (sourceTeam == -1)
            {
                Debug.LogError("[Server] Invalid team");
                continue;
            }

            foreach (var (ghostInstance, processor, team, commandDefinitions) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<Processor>, RefRO<UnitTeam>, DynamicBuffer<BufferedUnitCommandDefinition>>())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                if (team.ValueRO.Team != sourceTeam)
                {
                    Debug.LogError(string.Format("[Server] Can't send commands to units in other team. Source: {0} Target: {1}", sourceTeam, team.ValueRO.Team));
                    break;
                }

                for (int i = 0; i < commandDefinitions.Length; i++)
                {
                    if (commandDefinitions[i].Id == command.ValueRO.CommandId)
                    {
                        Unity.Collections.FixedBytes30 data = default;
                        nint dataPtr = (nint)(&data);
                        int dataLength = 0;
                        for (int j = 0; j < commandDefinitions[i].ParameterCount; j++)
                        {
                            BufferedUnitCommandDefinition commandDefinition = commandDefinitions[i];
                            switch (((UnitCommandParameter*)&commandDefinition.Parameters)[j])
                            {
                                case UnitCommandParameter.Position:
                                    {
                                        dataPtr.Set(new float2(command.ValueRO.WorldPosition.x, command.ValueRO.WorldPosition.z));
                                        dataPtr += sizeof(float2);
                                        dataLength += sizeof(float2);
                                        break;
                                    }
                            }
                        }
                        if (processor.ValueRW.CommandQueue.Length >= processor.ValueRW.CommandQueue.Capacity)
                        {
                            processor.ValueRW.CommandQueue.RemoveAt(0);
                            Debug.LogWarning("[Server] Too much commands");
                        }
                        processor.ValueRW.CommandQueue.Add(new UnitCommandRequest(command.ValueRO.CommandId, (ushort)dataLength, data));
                        break;
                    }
                }

                break;
            }
        }
    }
}
