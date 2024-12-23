using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct BuildingsSystemClient : ISystem
{
    public NativeList<BufferedBuilding> Buildings;

    void ISystem.OnCreate(ref SystemState state)
    {
        Buildings = new(Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<BuildingsResponseRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            DynamicBuffer<BufferedBuilding> units = SystemAPI.GetBuffer<BufferedBuilding>(SystemAPI.GetSingletonEntity<BuildingDatabase>());

            bool alreadyAdded = false;
            for (int i = 0; i < Buildings.Length; i++)
            {
                if (Buildings[i].Name != command.ValueRO.Name) continue;
                alreadyAdded = true;
                break;
            }

            if (!alreadyAdded)
            {
                for (int i = 0; i < units.Length; i++)
                {
                    if (command.ValueRO.Name != units[i].Name) continue;
                    Buildings.Add(units[i]);
                    break;
                }
            }
        }
    }

    public static ref BuildingsSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<BuildingsSystemClient>();
        return ref world.GetUnsafeSystemRef<BuildingsSystemClient>(handle);
    }

    public static void Refresh(in WorldUnmanaged world)
    {
        ref BuildingsSystemClient system = ref GetInstance(world);
        system.Buildings.Clear();

        EntityCommandBuffer commandBuffer = new(Allocator.Temp);

        Entity request = commandBuffer.CreateEntity();
        commandBuffer.AddComponent<SendRpcCommandRequest>(request);
        commandBuffer.AddComponent<BuildingsRequestRpc>(request);
        commandBuffer.Playback(world.EntityManager);
        commandBuffer.Dispose();

        Debug.Log("Request avaliable buildings ...");
    }
}
