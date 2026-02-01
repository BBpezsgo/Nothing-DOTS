using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
partial struct DisposeRpcSystemLocal : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (command, entity) in
            SystemAPI.Query<RefRW<ReceiveRpcCommandRequest>>()
            .WithEntityAccess())
        {
            if (command.ValueRO.Age >= 4)
            {
                using NativeArray<ComponentType> components = state.GetEntityStorageInfoLookup()[entity].Chunk.Archetype.GetComponentTypes(Unity.Collections.Allocator.Temp);
                foreach (ComponentType item in components)
                {
                    if (typeof(IRpcCommand).IsAssignableFrom(item.GetManagedType()))
                    {
                        Debug.LogError($"RPC entity automatically destroyed {item.GetManagedType()}");
                        goto k;
                    }
                }
                Debug.LogError("RPC entity automatically destroyed");
            k:
                commandBuffer.DestroyEntity(entity);
                continue;
            }
            command.ValueRW.Age++;
        }
    }
}
