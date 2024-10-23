using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct ClientSystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        
    }
}
