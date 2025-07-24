using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateBefore(typeof(RpcSystem))]
public partial struct LoadLevelSystem : ISystem
{
    PortableFunctionPointer<GhostImportance.ScaleImportanceDelegate> ScaleFunctionPointer;
    PortableFunctionPointer<GhostImportance.BatchScaleImportanceDelegate> BatchScaleFunction;

    public void OnCreate(ref SystemState state)
    {
        ScaleFunctionPointer = GhostDistanceImportance.ScaleFunctionPointer;
        BatchScaleFunction = GhostDistanceImportance.BatchScaleFunctionPointer;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        bool hasGhostImportanceScaling = SystemAPI.HasSingleton<GhostImportance>();
        if (hasGhostImportanceScaling) return;

        Entity gridSingleton = state.EntityManager.CreateSingleton(new GhostDistanceData()
        {
            TileSize = new int3(10, 10, 256),
            TileCenter = new int3(0, 0, 128),
            TileBorderWidth = new float3(1f, 1f, 1f),
        });
        state.EntityManager.AddComponentData<GhostImportance>(gridSingleton, new()
        {
            BatchScaleImportanceFunction = BatchScaleFunction,
            GhostConnectionComponentType = ComponentType.ReadOnly<GhostConnectionPosition>(),
            GhostImportanceDataType = ComponentType.ReadOnly<GhostDistanceData>(),
            GhostImportancePerChunkDataType = ComponentType.ReadOnly<GhostDistancePartitionShared>(),
        });
    }
}
