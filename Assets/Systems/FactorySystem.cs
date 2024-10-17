using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

        float now = UnityEngine.Time.time;

        foreach ((RefRW<Factory> factory, RefRO<LocalToWorld> localToWorld, Entity entity) in
                    SystemAPI.Query<RefRW<Factory>, RefRO<LocalToWorld>>()
                    .WithEntityAccess())
        {
            if (!_queueQ.TryGetBuffer(entity, out DynamicBuffer<BufferedUnit> unitQueue))
            { continue; }

            if (factory.ValueRO.CurrentFinishAt == default)
            {
                if (unitQueue.Length > 0)
                {
                    BufferedUnit unit = unitQueue[0];
                    unitQueue.RemoveAt(0);
                    factory.ValueRW.Current = unit;
                    factory.ValueRW.CurrentStartedAt = now;
                    factory.ValueRW.CurrentFinishAt = now + 10f / Factory.ProductionSpeed;
                }

                continue;
            }

            if (factory.ValueRO.CurrentFinishAt < now)
            {
                continue;
            }

            BufferedUnit finishedUnit = factory.ValueRO.Current;

            factory.ValueRW.Current = default;
            factory.ValueRW.CurrentStartedAt = default;
            factory.ValueRW.CurrentFinishAt = default;

            Entity newUnit = state.EntityManager.Instantiate(finishedUnit.Prefab);
            state.EntityManager.SetComponentData(newUnit, LocalTransform.FromPosition(localToWorld.ValueRO.Position + new float3(0f, 0f, 3f)));
        }
    }
}
