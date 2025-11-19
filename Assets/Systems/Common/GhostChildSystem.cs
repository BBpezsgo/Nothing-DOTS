using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
partial struct GhostChildSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (ghostChild, transform, entity) in
            SystemAPI.Query<RefRW<GhostChild>, RefRW<LocalTransform>>()
            .WithEntityAccess())
        {
            if (ghostChild.ValueRO.ParentEntity.Equals(ghostChild.ValueRO.LocalParentEntity)) continue;

            if (!ghostChild.ValueRO.ParentEntity.Equals(default))
            {
                foreach (var (parnetGhost, parentEntity) in
                    SystemAPI.Query<RefRO<GhostInstance>>()
                    .WithEntityAccess())
                {
                    if (parnetGhost.ValueRO.Equals(ghostChild.ValueRO.ParentEntity))
                    {
                        if (!SystemAPI.HasComponent<Parent>(entity))
                        {
                            commandBuffer.AddComponent<Parent>(entity, new()
                            {
                                Value = parentEntity,
                            });
                        }
                        else
                        {
                            Debug.LogWarning("Parent component already added");
                            commandBuffer.SetComponent<Parent>(entity, new()
                            {
                                Value = parentEntity,
                            });
                        }
                        ghostChild.ValueRW.LocalParentEntity = parnetGhost.ValueRO;
                        transform.ValueRW.Position = ghostChild.ValueRO.LocalPosition;
                        transform.ValueRW.Rotation = ghostChild.ValueRO.LocalRotation;
                        goto good;
                    }
                }
                Debug.LogError(string.Format("Ghost parent {0} does not exists", ghostChild.ValueRO.ParentEntity));
            good:;
            }

            if (ghostChild.ValueRO.ParentEntity.Equals(default) &&
                !ghostChild.ValueRO.LocalParentEntity.Equals(default))
            {
                if (SystemAPI.HasComponent<Parent>(entity))
                {
                    commandBuffer.RemoveComponent<Parent>(entity);
                }
                ghostChild.ValueRW.LocalParentEntity = default;
            }
        }
    }
}
