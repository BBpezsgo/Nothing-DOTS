using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct PendriveProcessorSystem : ISystem
{
    Random _random;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        _random = Random.CreateFromIndex(420);
    }

    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (processor, transform, localTransform, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalToWorld>, RefRO<LocalTransform>>()
            .WithEntityAccess())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            if (processor.ValueRO.PendrivePlugRequested)
            {
                processor.ValueRW.PendrivePlugRequested = false;

                if (processor.ValueRO.PluggedPendrive.Entity == Entity.Null)
                {
                    foreach (var (pendrive, pendriveTransform, pendriveLocalTransform, rigidbody, pendriveEntity) in
                        SystemAPI.Query<RefRO<Pendrive>, RefRO<LocalToWorld>, RefRW<LocalTransform>, RefRW<Rigidbody>>()
                        .WithEntityAccess())
                    {
                        if (math.distancesq(pendriveTransform.ValueRO.Position, transform.ValueRO.Position) >= 5f * 5f) continue;

                        processor.ValueRW.PluggedPendrive = (false, pendrive.ValueRO, pendriveEntity);

                        commandBuffer.AddComponent<Parent>(pendriveEntity, new()
                        {
                            Value = entity
                        });
                        pendriveLocalTransform.ValueRW.Position = new float3(0f, 1f, 0f);
                        rigidbody.ValueRW.IsEnabled = false;
                    }
                }
            }

            if (processor.ValueRO.PluggedPendrive.Write)
            {
                processor.ValueRW.PluggedPendrive.Write = false;

                if (processor.ValueRO.PluggedPendrive.Entity != Entity.Null)
                {
                    if (state.EntityManager.Exists(processor.ValueRO.PluggedPendrive.Entity))
                    {
                        SystemAPI.GetComponentRW<Pendrive>(processor.ValueRO.PluggedPendrive.Entity).ValueRW.Data = processor.ValueRW.PluggedPendrive.Pendrive.Data;
                    }
                    else
                    {
                        Debug.LogError(string.Format("Pendrive entity {0} does not exists", processor.ValueRO.PluggedPendrive.Entity));
                    }
                }
            }

            if (processor.ValueRO.PendriveUnplugRequested)
            {
                processor.ValueRW.PendriveUnplugRequested = false;

                if (processor.ValueRO.PluggedPendrive.Entity != Entity.Null)
                {
                    foreach (var (pendrive, pendriveTransform, pendriveParent, rigidbody, pendriveEntity) in
                        SystemAPI.Query<RefRO<Pendrive>, RefRO<LocalToWorld>, RefRO<Parent>, RefRW<Rigidbody>>()
                        .WithEntityAccess())
                    {
                        if (pendriveParent.ValueRO.Value != entity) continue;

                        processor.ValueRW.PluggedPendrive = default;

                        commandBuffer.RemoveComponent<Parent>(pendriveEntity);
                        rigidbody.ValueRW.Velocity = new float3(_random.NextFloat3Direction() * 5f);
                        rigidbody.ValueRW.Velocity.y = math.abs(rigidbody.ValueRW.Velocity.y);
                        rigidbody.ValueRW.IsEnabled = true;
                        break;
                    }
                }
            }
        }
    }
}
