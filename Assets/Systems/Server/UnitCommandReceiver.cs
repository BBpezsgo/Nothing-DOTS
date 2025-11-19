using System;
using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Collections;
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
            NetworkId networkId = request.ValueRO.SourceConnection == default ? default : SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection).ValueRO;

            int sourceTeam = -1;
            foreach (var player in
                SystemAPI.Query<RefRO<Player>>())
            {
                if (player.ValueRO.ConnectionId != networkId.Value) continue;
                sourceTeam = player.ValueRO.Team;
                break;
            }

            if (sourceTeam == -1)
            {
                Debug.LogError("[Server] Invalid team");
                continue;
            }

            foreach (var (ghostInstance, processor, team) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<Processor>, RefRO<UnitTeam>>())
            {
                if (!command.ValueRO.Entity.Equals(ghostInstance.ValueRO)) continue;

                if (team.ValueRO.Team != sourceTeam)
                {
                    Debug.LogError(string.Format("[Server] Can't send commands to units in other team. Source: {0} Target: {1}", sourceTeam, team.ValueRO.Team));
                    break;
                }

                ReadOnlySpan<UnitCommandDefinition> commandDefinitions = processor.ValueRO.Source.UnitCommandDefinitions.AsSpan();

                for (int i = 0; i < commandDefinitions.Length; i++)
                {
                    if (commandDefinitions[i].Id == command.ValueRO.CommandId)
                    {
                        FixedBytes30 data = default;
                        nint dataPtr = (nint)(&data);
                        int dataLength = 0;
                        for (int j = 0; j < commandDefinitions[i].ParameterCount; j++)
                        {
                            switch (commandDefinitions[i].GetParameter(j))
                            {
                                case UnitCommandParameter.Position2:
                                {
                                    if (command.ValueRO.WorldPosition.Equals(default))
                                    {
                                        Debug.LogWarning("[Server] Position data not provided");
                                        goto failed;
                                    }

                                    dataPtr.Set(new float2(command.ValueRO.WorldPosition.x, command.ValueRO.WorldPosition.z));
                                    dataPtr += sizeof(float2);
                                    dataLength += sizeof(float2);
                                    break;
                                }
                                case UnitCommandParameter.Position3:
                                {
                                    if (command.ValueRO.WorldPosition.Equals(default))
                                    {
                                        Debug.LogWarning("[Server] Position data not provided");
                                        goto failed;
                                    }

                                    dataPtr.Set(command.ValueRO.WorldPosition);
                                    dataPtr += sizeof(float3);
                                    dataLength += sizeof(float3);
                                    break;
                                }
                                default:
                                    throw new UnreachableException();
                            }
                        }
                        if (processor.ValueRW.CommandQueue.Length >= processor.ValueRW.CommandQueue.Capacity)
                        {
                            processor.ValueRW.CommandQueue.RemoveAt(0);
                            Debug.LogWarning("[Server] Too much commands");
                        }

                        processor.ValueRW.CommandQueue.Add(new UnitCommandRequest(command.ValueRO.CommandId, (ushort)dataLength, data));

                    failed:
                        break;
                    }
                }

                break;
            }
        }
    }
}
