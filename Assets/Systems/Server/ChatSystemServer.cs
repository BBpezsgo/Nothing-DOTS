using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ChatSystemServer : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ChatMessageRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            foreach (var (id, entity2) in
                SystemAPI.Query<RefRO<NetworkId>>()
                .WithAll<InitializedClient>()
                .WithEntityAccess())
            {
                if (entity == entity2) continue;
                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new ChatMessageRpc()
                {
                    Sender = networkId.ValueRO.Value,
                    Message = command.ValueRO.Message,
                    Time = command.ValueRO.Time,
                });
            }
        }
    }

    [BurstCompile]
    public static void SendChatMessage(in EntityCommandBuffer commandBuffer, in WorldUnmanaged world, in FixedString64Bytes message, long time)
    {
        NetcodeUtils.CreateRPC(in commandBuffer, in world, new ChatMessageRpc()
        {
            Sender = 0,
            Message = message,
            Time = time,
        });
    }
}
