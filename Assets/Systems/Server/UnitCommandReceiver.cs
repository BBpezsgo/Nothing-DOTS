using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct UnitCommandReceiver : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<UnitCommandRequestRpc>>()
            .WithEntityAccess())
        {
            foreach (var (ghostInstance, processor, commandDefinitions, ghostEntity) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<Processor>, DynamicBuffer<BufferedUnitCommandDefinition>>()
                .WithEntityAccess())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.Entity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.Entity.spawnTick) continue;

                for (int i = 0; i < commandDefinitions.Length; i++)
                {
                    if (commandDefinitions[i].Id == command.ValueRO.CommandId)
                    {
                        Unity.Collections.FixedBytes30 data = default;
                        nint dataPtr = (nint)(&data);
                        int dataLength = 0;
                        for (int j = 0; j < commandDefinitions[i].Parameters.Length; j++)
                        {
                            switch (commandDefinitions[i].Parameters[j])
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
                            Debug.LogWarning("Too much commands");
                        }
                        processor.ValueRW.CommandQueue.Add(new UnitCommandRequest(command.ValueRO.CommandId, (ushort)dataLength, data));
                        break;
                    }
                }

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
