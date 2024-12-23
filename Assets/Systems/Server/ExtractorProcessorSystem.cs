using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct ExtractorProcessorSystem : ISystem
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
        PrefabDatabase prefabDatabase = SystemAPI.GetSingleton<PrefabDatabase>();
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (processor, extractor, transform, localTransform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Extractor>, RefRO<LocalToWorld>, RefRO<LocalTransform>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            if (mapped->Extractor.InputExtract != 0)
            {
                mapped->Extractor.InputExtract = 0;

                foreach (var resourceNodeTransform in
                    SystemAPI.Query<RefRO<LocalToWorld>>()
                    .WithAll<ResourceNode>())
                {
                    float distanceSq = math.distancesq(resourceNodeTransform.ValueRO.Position, transform.ValueRO.Position);
                    if (distanceSq > Extractor.ExtractRadius * Extractor.ExtractRadius) continue;

                    Debug.DrawLine(transform.ValueRO.Position, resourceNodeTransform.ValueRO.Position, Color.green, .1f);

                    extractor.ValueRW.ExtractProgress += Extractor.ExtractSpeed * SystemAPI.Time.DeltaTime;

                    if (extractor.ValueRO.ExtractProgress < 1f) continue;
                    extractor.ValueRW.ExtractProgress--;

                    bool extracted = false;

                    foreach (var (resource, resourceTransform) in
                        SystemAPI.Query<RefRW<Resource>, RefRO<LocalToWorld>>())
                    {
                        distanceSq = math.distancesq(resourceTransform.ValueRO.Position, transform.ValueRO.Position);
                        if (distanceSq > Extractor.ExtractRadius * Extractor.ExtractRadius) continue;
                        if (resource.ValueRO.Amount >= Resource.Capacity) continue;
                        resource.ValueRW.Amount++;
                        extracted = true;
                        Debug.DrawLine(transform.ValueRO.Position, resourceTransform.ValueRO.Position, Color.cyan, .1f);
                        break;
                    }

                    if (!extracted)
                    {
                        Entity newResource = commandBuffer.Instantiate(prefabDatabase.Resource);
                        commandBuffer.SetComponent<Resource>(newResource, new()
                        {
                            Amount = 1,
                        });
                        commandBuffer.SetComponent<LocalTransform>(newResource, LocalTransform.FromPosition(localTransform.ValueRO.TransformPoint(extractor.ValueRO.ExtractPoint)));
                        float3 velocity = _random.NextFloat3Direction() * 0.5f;
                        velocity.y = _random.NextFloat(2f, 5f);
                        commandBuffer.SetComponent<Rigidbody>(newResource, new()
                        {
                            Velocity = velocity,
                        });
                        Debug.DrawLine(transform.ValueRO.Position, transform.ValueRO.Position + new float3(0f, 0f, 2f), Color.blue, 1f);
                    }

                    break;
                }
            }
        }
    }
}
