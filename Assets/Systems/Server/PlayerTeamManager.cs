using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct PlayerTeamManager : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (id, entity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<PlayerTeam>()
            .WithEntityAccess())
        {
            commandBuffer.AddComponent<PlayerTeam>(entity, new()
            {
                Team = id.ValueRO.Value,
            });
        }
        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
