using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ResearchSystemServer : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ResearchesRequestRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);
            RefRO<NetworkId> networkId = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);

            Entity requestPlayer = default;

            foreach (var (player, _entity) in
                SystemAPI.Query<RefRO<Player>>()
                .WithEntityAccess())
            {
                if (player.ValueRO.ConnectionId != networkId.ValueRO.Value) continue;
                requestPlayer = _entity;
                break;
            }

            if (requestPlayer == Entity.Null)
            {
                Debug.LogError(string.Format("Player with network id {0} aint have a team", networkId));
                continue;
            }

            DynamicBuffer<BufferedAcquiredResearch> acquiredResearches = SystemAPI.GetBuffer<BufferedAcquiredResearch>(requestPlayer);

            foreach (var (_research, requirements) in
                SystemAPI.Query<RefRO<Research>, DynamicBuffer<BufferedResearchRequirement>>())
            {
                bool hasAllRequirements = true;

                foreach (BufferedResearchRequirement requirement in requirements)
                {
                    bool hasThis = false;

                    foreach (BufferedAcquiredResearch acquired in acquiredResearches)
                    {
                        if (requirement.Name != acquired.Name) continue;
                        hasThis = true;
                        break;
                    }

                    if (!hasThis)
                    {
                        hasAllRequirements = false;
                        break;
                    }
                }
                if (!hasAllRequirements) continue;

                bool alreadyResearched = false;
                foreach (BufferedAcquiredResearch acquired in acquiredResearches)
                {
                    if (_research.ValueRO.Name != acquired.Name) continue;
                    alreadyResearched = true;
                    break;
                }
                if (alreadyResearched) continue;

                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<ResearchesResponseRpc>(response, new()
                {
                    Name = _research.ValueRO.Name,
                });
            }
        }
    }
}
