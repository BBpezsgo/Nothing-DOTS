using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct GoInGameClientSystem : ISystem
{
    void ISystem.OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        builder.WithAny<NetworkId>();
        builder.WithNone<NetworkStreamInGame>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        foreach (var (id, entity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<NetworkStreamInGame>()
            .WithEntityAccess())
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(entity);
            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<GoInGameRpcCommand>(request);
            commandBuffer.AddComponent<SendRpcCommandRequest>(request);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
