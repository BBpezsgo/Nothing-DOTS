using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FactorySystem : ISystem
{
    BufferLookup<BufferedUnit> _queueQ;

    void ISystem.OnCreate(ref SystemState state)
    {
        _queueQ = state.GetBufferLookup<BufferedUnit>();
    }

    // [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        _queueQ.Update(ref state);

        if (!SystemAPI.TryGetSingletonEntity<UnitDatabase>(out Entity unitDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

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

                BufferedUnit unit = units.FirstOrDefault(v => v.Name == command.ValueRO.Unit);

                if (unit.Prefab == Entity.Null)
                {
                    Debug.LogWarning($"Unit \"{command.ValueRO.Unit}\" not found in the database");
                    return;
                }

                SystemAPI.GetBuffer<BufferedUnit>(ghostEntity).Add(unit);

                break;
            }

            commandBuffer.DestroyEntity(entity);
        }

        foreach ((RefRW<Factory> factory, RefRO<LocalToWorld> localToWorld, Entity entity) in
                    SystemAPI.Query<RefRW<Factory>, RefRO<LocalToWorld>>()
                    .WithEntityAccess())
        {
            if (!_queueQ.TryGetBuffer(entity, out DynamicBuffer<BufferedUnit> unitQueue))
            { continue; }

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
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
