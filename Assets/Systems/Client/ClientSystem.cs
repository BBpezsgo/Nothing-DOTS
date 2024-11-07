using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct ClientSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {

    }
}
