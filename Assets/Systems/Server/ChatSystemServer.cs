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
                Entity rpc = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(rpc);
                commandBuffer.AddComponent<ChatMessageRpc>(rpc, new()
                {
                    Sender = networkId.ValueRO.Value,
                    Message = command.ValueRO.Message,
                });
            }
        }
    }

    public static void SendChatMessage(in EntityManager entityManager, FixedString64Bytes message)
    {
        Entity entity = entityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(FacilityQueueResearchRequestRpc));
        entityManager.SetComponentData(entity, new ChatMessageRpc()
        {
            Sender = 0,
            Message = message,
        });
    }

    public static void SendChatMessage(in EntityCommandBuffer commandBuffer, FixedString64Bytes message)
    {
        Entity entity = commandBuffer.CreateEntity();
        commandBuffer.AddComponent<SendRpcCommandRequest>(entity, new());
        commandBuffer.AddComponent<ChatMessageRpc>(entity, new()
        {
            Sender = 0,
            Message = message,
        });
    }
}
