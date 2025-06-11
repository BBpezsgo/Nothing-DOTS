using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct ResearchSystemClient : ISystem
{
    public NativeList<FixedString64Bytes> AvaliableResearches;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006")] class _k { }
    public static readonly SharedStatic<float> LastSynced = SharedStatic<float>.GetOrCreate<BuildingsSystemClient, _k>();

    void ISystem.OnCreate(ref SystemState state)
    {
        AvaliableResearches = new NativeList<FixedString64Bytes>(Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<ResearchesResponseRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            bool alreadyAdded = false;
            for (int i = 0; i < AvaliableResearches.Length; i++)
            {
                if (AvaliableResearches[i] != command.ValueRO.Name) continue;
                alreadyAdded = true;
                break;
            }

            if (!alreadyAdded)
            {
                AvaliableResearches.Add(command.ValueRO.Name);
                LastSynced.Data = MonoTime.Now;
            }
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<ResearchDoneRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            Entity request = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<SendRpcCommandRequest>(request);
            commandBuffer.AddComponent<ResearchesRequestRpc>(request);

            for (int i = 0; i < AvaliableResearches.Length; i++)
            {
                if (AvaliableResearches[i] != command.ValueRO.Name) continue;
                AvaliableResearches.RemoveAt(i);
                LastSynced.Data = MonoTime.Now;
                break;
            }
        }
    }

    public static ref ResearchSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<ResearchSystemClient>();
        return ref world.GetUnsafeSystemRef<ResearchSystemClient>(handle);
    }

    public static void Refresh(in WorldUnmanaged world)
    {
        ref ResearchSystemClient system = ref GetInstance(world);
        system.AvaliableResearches.Clear();

        Entity request = world.EntityManager.CreateEntity(stackalloc ComponentType[]
        {
            typeof(SendRpcCommandRequest),
            typeof(ResearchesRequestRpc),
        });

        Debug.Log("[Client] Request avaliable researches ...");
    }
}
