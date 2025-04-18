using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct PlayerPositionSystemClient : ISystem
{
    float3 SyncedPosition;
    public float3 CurrentPosition;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (CurrentPosition.Equals(SyncedPosition)) return;
        SyncedPosition = CurrentPosition;

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        Entity rpc = commandBuffer.CreateEntity();
        commandBuffer.AddComponent<SendRpcCommandRequest>(rpc);
        commandBuffer.AddComponent<PlayerPositionSyncRpc>(rpc, new()
        {
            Position = CurrentPosition,
        });
    }

    public static ref PlayerPositionSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<PlayerPositionSystemClient>();
        return ref world.GetUnsafeSystemRef<PlayerPositionSystemClient>(handle);
    }
}
