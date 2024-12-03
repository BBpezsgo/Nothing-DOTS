using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FactorySystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitDatabase>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        Entity unitDatabase = SystemAPI.GetSingletonEntity<UnitDatabase>();
        DynamicBuffer<BufferedUnit> units = SystemAPI.GetBuffer<BufferedUnit>(unitDatabase);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<FactoryQueueUnitRequestRpc>>()
            .WithEntityAccess())
        {
            foreach (var (ghostInstance, ghostEntity) in
                SystemAPI.Query<RefRO<GhostInstance>>()
                .WithEntityAccess())
            {
                if (ghostInstance.ValueRO.ghostId != command.ValueRO.FactoryEntity.ghostId) continue;
                if (ghostInstance.ValueRO.spawnTick != command.ValueRO.FactoryEntity.spawnTick) continue;

                BufferedUnit unit = default;
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i].Name == command.ValueRO.Unit)
                    {
                        unit = units[i];
                        break;
                    }
                }

                if (unit.Prefab == Entity.Null)
                {
                    Debug.LogWarning($"Unit \"{command.ValueRO.Unit}\" not found in the database");
                    break;
                }

                SystemAPI.GetBuffer<BufferedUnit>(ghostEntity).Add(unit);

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach ((RefRW<Factory> factory, RefRO<LocalToWorld> localToWorld, RefRO<UnitTeam> unitTeam, Entity entity) in
                    SystemAPI.Query<RefRW<Factory>, RefRO<LocalToWorld>, RefRO<UnitTeam>>()
                    .WithEntityAccess())
        {
            DynamicBuffer<BufferedUnit> unitQueue = SystemAPI.GetBuffer<BufferedUnit>(entity);

            if (factory.ValueRO.TotalProgress == default)
            {
                if (unitQueue.Length > 0)
                {
                    BufferedUnit unit = unitQueue[0];
                    unitQueue.RemoveAt(0);
                    factory.ValueRW.Current = unit;
                    factory.ValueRW.CurrentProgress = 0f;
                    factory.ValueRW.TotalProgress = unit.ProductionTime;
                }

                continue;
            }

            factory.ValueRW.CurrentProgress += SystemAPI.Time.DeltaTime * Factory.ProductionSpeed;

            if (factory.ValueRO.CurrentProgress < factory.ValueRO.TotalProgress)
            { continue; }

            BufferedUnit finishedUnit = factory.ValueRO.Current;

            factory.ValueRW.Current = default;
            factory.ValueRW.CurrentProgress = default;
            factory.ValueRW.TotalProgress = default;

            Entity newUnit = commandBuffer.Instantiate(finishedUnit.Prefab);
            commandBuffer.SetComponent(newUnit, LocalTransform.FromPosition(localToWorld.ValueRO.Position + new float3(0f, 0f, 1.5f)));
            commandBuffer.SetComponent<UnitTeam>(newUnit, new()
            {
                Team = unitTeam.ValueRO.Team
            });
        }
    }
}
