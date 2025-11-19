using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial struct PendriveProcessorSystem : ISystem
{
    Random _random;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        _random = Random.CreateFromIndex(420);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, transform, localTransform, ghostInstance, entity) in
            SystemAPI.Query<RefRW<Processor>, RefRO<LocalToWorld>, RefRO<LocalTransform>, RefRO<GhostInstance>>()
            .WithEntityAccess())
        {
            if (processor.ValueRO.PendrivePlugRequested)
            {
                processor.ValueRW.PendrivePlugRequested = false;

                if (processor.ValueRO.PluggedPendrive.Entity == Entity.Null)
                {
                    foreach (var (pendrive, pendriveTransform, pendriveLocalTransform, rigidbody, collider, pendriveChild, pendriveEntity) in
                        SystemAPI.Query<RefRO<Pendrive>, RefRO<LocalToWorld>, RefRW<LocalTransform>, RefRW<Rigidbody>, RefRW<Collider>, RefRW<GhostChild>>()
                        .WithEntityAccess())
                    {
                        if (math.distancesq(pendriveTransform.ValueRO.Position, transform.ValueRO.Position) >= 5f * 5f) continue;

                        processor.ValueRW.PluggedPendrive = (false, pendrive.ValueRO, pendriveEntity);
                        pendriveChild.ValueRW.ParentEntity = ghostInstance.ValueRO;
                        pendriveChild.ValueRW.LocalPosition = processor.ValueRO.USBPosition;
                        pendriveChild.ValueRW.LocalRotation = processor.ValueRO.USBRotation;
                        rigidbody.ValueRW.IsEnabled = false;
                        collider.ValueRW.IsEnabled = false;
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
                        Debug.LogError(string.Format("[Server] Pendrive entity {0} does not exists", processor.ValueRO.PluggedPendrive.Entity));
                    }
                }
            }

            if (processor.ValueRO.PendriveUnplugRequested)
            {
                processor.ValueRW.PendriveUnplugRequested = false;

                if (processor.ValueRO.PluggedPendrive.Entity != Entity.Null)
                {
                    foreach (var (pendrive, pendriveTransform, pendriveParent, rigidbody, collider, pendriveChild, pendriveEntity) in
                        SystemAPI.Query<RefRO<Pendrive>, RefRW<LocalTransform>, RefRO<Parent>, RefRW<Rigidbody>, RefRW<Collider>, RefRW<GhostChild>>()
                        .WithEntityAccess())
                    {
                        if (pendriveParent.ValueRO.Value != entity) continue;

                        processor.ValueRW.PluggedPendrive = default;
                        pendriveChild.ValueRW.ParentEntity = default;
                        rigidbody.ValueRW.Velocity = new float3(_random.NextFloat3Direction() * 1f);
                        rigidbody.ValueRW.Velocity.y = math.abs(rigidbody.ValueRW.Velocity.y);
                        rigidbody.ValueRW.IsEnabled = true;
                        collider.ValueRW.IsEnabled = true;
                        break;
                    }
                }
            }
        }
    }
}
