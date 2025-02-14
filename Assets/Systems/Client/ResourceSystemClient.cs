using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct ResourceSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (resource, transform) in
            SystemAPI.Query<RefRO<Resource>, RefRW<LocalTransform>>())
        {
            transform.ValueRW.Scale =
                resource.ValueRO.InitialScale *
                math.lerp(0.2f, 1f, math.clamp((float)resource.ValueRO.Amount / (float)Resource.Capacity, 0f, 1f));
        }
    }
}
