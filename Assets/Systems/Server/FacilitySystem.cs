using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FacilitySystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitDatabase>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FacilityQueueResearchRequestRpc>>()
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
                Debug.LogError(string.Format("[Server] Failed to start research: requested by {0} but aint have a team", networkId.ValueRO));
                return;
            }

            var acquiredResearches = SystemAPI.GetBuffer<BufferedAcquiredResearch>(requestPlayer);

            foreach (var (ghostInstance, ghostEntity) in
                SystemAPI.Query<RefRO<GhostInstance>>()
                .WithAll<Facility>()
                .WithEntityAccess())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.FacilityEntity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.FacilityEntity.spawnTick) continue;

                Research research = default;
                bool canResearch = false;

                foreach (var (_research, requirements) in
                    SystemAPI.Query<RefRO<Research>, DynamicBuffer<BufferedResearchRequirement>>())
                {
                    if (_research.ValueRO.Name != command.ValueRO.ResearchName) continue;
                    research = _research.ValueRO;
                    canResearch = true;

                    foreach (var requirement in requirements)
                    {
                        bool has = false;
                        foreach (var acquired in acquiredResearches)
                        {
                            if (requirement.Name == acquired.Name)
                            {
                                has = true;
                                break;
                            }
                        }
                        if (!has)
                        {
                            canResearch = false;
                        }
                    }

                    break;
                }

                if (research.Name.IsEmpty)
                {
                    Debug.LogWarning($"[Server] Research \"{command.ValueRO.ResearchName}\" not found in the database");
                    break;
                }

                if (!canResearch)
                {
                    Debug.LogWarning($"[Server] Research \"{command.ValueRO.ResearchName}\" cannot be started");
                    break;
                }

                bool alreadyHas = false;
                foreach (var acquired in acquiredResearches)
                {
                    if (research.Name == acquired.Name)
                    {
                        alreadyHas = true;
                        break;
                    }
                }

                if (alreadyHas)
                {
                    Debug.LogWarning($"[Server] Research \"{research.Name}\" already acquired");
                    break;
                }

                SystemAPI.GetBuffer<BufferedResearch>(ghostEntity).Add(new BufferedResearch()
                {
                    Name = research.Name,
                    Hash = research.Hash,
                    ResearchTime = research.ResearchTime,
                });

                break;
            }
        }

        foreach (var (facility, unitTeam, queue, hashIn, hashOut) in
            SystemAPI.Query<RefRW<Facility>, RefRO<UnitTeam>, DynamicBuffer<BufferedResearch>, DynamicBuffer<BufferedTechnologyHashIn>, DynamicBuffer<BufferedTechnologyHashOut>>())
        {
            Research finishedResearch;

            if (!hashIn.IsEmpty)
            {
                BufferedTechnologyHashIn hash = hashIn[0];
                hashIn.RemoveAt(0);

                foreach (var research in
                    SystemAPI.Query<RefRO<Research>>())
                {
                    if (FixedBytes.AreEqual(research.ValueRO.Hash, hash.Hash))
                    {
                        finishedResearch = research.ValueRO;
                        goto good;
                    }
                }

                Debug.LogError(string.Format("[Server] Research with hash \"{0}\" not found", hash.Hash));
                continue;
                good:;
            }
            else
            {
                if (facility.ValueRO.Current.Name.IsEmpty)
                {
                    if (queue.Length > 0)
                    {
                        BufferedResearch research = queue[0];
                        queue.RemoveAt(0);
                        facility.ValueRW.Current = research;
                        facility.ValueRW.CurrentProgress = 0f;
                    }

                    continue;
                }

                facility.ValueRW.CurrentProgress += SystemAPI.Time.DeltaTime * Facility.ResearchSpeed;

                if (facility.ValueRO.CurrentProgress < facility.ValueRO.Current.ResearchTime)
                { continue; }

                finishedResearch = new Research()
                {
                    Hash = facility.ValueRO.Current.Hash,
                    Name = facility.ValueRO.Current.Name,
                    ResearchTime = facility.ValueRO.Current.ResearchTime,
                };

                facility.ValueRW.Current = default;
                facility.ValueRW.CurrentProgress = default;

                hashOut.Add(new BufferedTechnologyHashOut()
                {
                    Hash = finishedResearch.Hash,
                });
            }

            Entity playerEntity = default;
            Player player = default;

            foreach (var (_player, _entity) in
                SystemAPI.Query<RefRO<Player>>()
                .WithEntityAccess())
            {
                if (_player.ValueRO.Team != unitTeam.ValueRO.Team) continue;
                playerEntity = _entity;
                player = _player.ValueRO;
                break;
            }

            if (playerEntity == Entity.Null)
            {
                Debug.LogError(string.Format("[Server] Failed to finish research: No player found im team {0}", unitTeam.ValueRO.Team));
                return;
            }

            Entity playerConnection = default;
            foreach (var (networkId, _entity) in
                SystemAPI.Query<RefRO<NetworkId>>()
                .WithEntityAccess())
            {
                if (networkId.ValueRO.Value != player.ConnectionId) continue;
                playerConnection = _entity;
                break;
            }

            if (playerConnection != Entity.Null)
            {
                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = playerConnection,
                });
                commandBuffer.AddComponent<ResearchDoneRpc>(response, new()
                {
                    Name = finishedResearch.Name,
                });
            }

            SystemAPI.GetBuffer<BufferedAcquiredResearch>(playerEntity)
                .Add(new BufferedAcquiredResearch()
                {
                    Name = finishedResearch.Name,
                });
        }
    }
}
