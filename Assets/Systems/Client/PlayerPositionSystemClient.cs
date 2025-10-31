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
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        if (CurrentPosition.Equals(SyncedPosition)) return;
        SyncedPosition = CurrentPosition;

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new PlayerPositionSyncRpc()
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
