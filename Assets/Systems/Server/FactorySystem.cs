using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct FactorySystem : ISystem
{
    BufferLookup<BufferedUnit> _queueQ;

    void ISystem.OnCreate(ref SystemState state)
    {
        _queueQ = state.GetBufferLookup<BufferedUnit>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        _queueQ.Update(ref state);
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

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
