using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using System;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct TerrainSystemServer : ISystem
{
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatshadedChunkSizes = 3;

    public const float meshScale = 10f;
    public const bool useFlatShading = false;

    public const float cellSize = meshScale / (NumVertsPerLine - 1);

    public const int NumVertsPerLine = 72 + 1;
    //    0 switch
    //    {
    //        0 => 48,
    //        1 => 72,
    //        2 => 96,
    //        3 => 120,
    //        4 => 144,
    //        5 => 168,
    //        6 => 192,
    //        7 => 216,
    //        8 => 240,
    //        _ => throw new IndexOutOfRangeException(),
    //    } + 1;

    public static readonly float MeshWorldSize = (NumVertsPerLine - 3) * meshScale;

    public static readonly float DataPointWorldSize = 1f / (NumVertsPerLine - 3) * MeshWorldSize;

    NativeHashMap<int2, NativeArray<float>.ReadOnly> Heightmaps;
    NativeList<int2> Queue;
    NativeHashSet<int2> Hashset;

    [BurstCompile]
    public static ref TerrainSystemServer GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<TerrainSystemServer>();
        return ref world.GetUnsafeSystemRef<TerrainSystemServer>(handle);
    }

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        Heightmaps = new NativeHashMap<int2, NativeArray<float>.ReadOnly>(32, Allocator.Persistent);
        Queue = new NativeList<int2>(Allocator.Persistent);
        Hashset = new NativeHashSet<int2>(4, Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        //foreach (var item in Heightmaps)
        //{
        //    float2 p = ChunkToWorld(item.Key);
        //    DebugEx.DrawBoxAligned(new float3(p.x, 0, p.y), new float3(MeshWorldSize, 0f, MeshWorldSize), Color.red, 0f, false);
        //}

        if (Queue.Length == 0) return;

        NativeHeightMapSettings heightMapSettings = SystemAPI.GetSingleton<NativeHeightMapSettings>();
        TerrainFeatures terrainFeatures = SystemAPI.GetSingleton<TerrainFeatures>();

        NativeArray<NativeArray<float>> results = new(Queue.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = new NativeArray<float>(NumVertsPerLine * NumVertsPerLine, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        TerrainGeneratorJobServer task = new()
        {
            Result = results.AsReadOnly(),
            Queue = Queue.AsReadOnly(),
            HeightMapSettings = heightMapSettings,
        };
        JobHandle handle = task.ScheduleParallel(Queue.Length, 4, default);
        handle.Complete();

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        for (int i = 0; i < Queue.Length; i++)
        {
            GenerateChunkFeatures(
                Queue[i],
                results[i],
                in terrainFeatures,
                ref commandBuffer
            );
        }

        for (int i = 0; i < results.Length; i++)
        {
            Heightmaps[Queue[i]] = results[i].AsReadOnly();
            //for (int y = 0; y < NumVertsPerLine - 1; y++)
            //{
            //    for (int x = 0; x < NumVertsPerLine - 1; x++)
            //    {
            //        Sample(results[i], ChunkToWorld(Queue[i]) + DataToWorld(new int2(x, y)), Queue[i], new int2(x, y), out _);
            //    }
            //}
        }

        Hashset.Clear();
        Queue.Clear();
        results.Dispose();
    }

    [BurstCompile]
    public static void GenerateChunkFeatures(
        in int2 chunkCoord,
        in ReadOnlySpan<float> heightmap,
        in TerrainFeatures terrainFeatures,
        ref EntityCommandBuffer commandBuffer)
    {
        float2 chunkOrigin = ChunkToWorld(chunkCoord);
        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)math.abs(chunkCoord.x) + (uint)math.abs(chunkCoord.y));
        int n = random.NextInt(1, 8);
        for (int j = 0; j < n; j++)
        {
            int2 dataCoord = random.NextInt2(default, new int2(NumVertsPerLine - 1));
            float2 randomPosition = DataToWorld(dataCoord) + chunkOrigin + random.NextFloat2(new float2(-cellSize / 2, -cellSize / 2), new float2(cellSize / 2, cellSize / 2));
            float height = Sample(heightmap, randomPosition, chunkCoord, dataCoord, out float3 normal);
            Entity newResource = commandBuffer.Instantiate(terrainFeatures.ResourcePrefab);
            float3 p = new(
                randomPosition.x,
                height,
                randomPosition.y
            );
            commandBuffer.SetComponent(newResource, LocalTransform.FromPosition(p));
            //DebugEx.DrawSphere(p, 5f, Color.red, 1000f);
        }
    }

    [BurstCompile]
    public static float2 ChunkToWorld(int2 chunkCoord) => (float2)chunkCoord * MeshWorldSize;

    [BurstCompile]
    public static int2 WorldToChunk(float2 worldPosition) => (int2)math.round(worldPosition / MeshWorldSize);

    static readonly float2 _topLeft = new float2(-1, 1) * MeshWorldSize / 2f;

    /// <summary>
    /// Chunk relative world to data coord
    /// </summary>
    [BurstCompile]
    public static int2 WorldToData(float2 pos)
        => (int2)((pos - _topLeft) * new float2(1f, -1f) / MeshWorldSize * (NumVertsPerLine - 3)) + new int2(1, 1);

    /// <summary>
    /// Data coord to chunk relative world
    /// </summary>
    [BurstCompile]
    public static float2 DataToWorld(int2 pos)
        => _topLeft + new float2(pos.x - 1, pos.y - 1) / (NumVertsPerLine - 3) * new float2(1f, -1f) * MeshWorldSize;

    [BurstCompile]
    public bool TrySample(float2 position, out float height, out float3 normal, bool neighbours = false)
    {
        int2 chunkCoord = WorldToChunk(position + new float2(DataPointWorldSize, DataPointWorldSize) * 0.5f);

        if (!Heightmaps.TryGetValue(chunkCoord, out NativeArray<float>.ReadOnly heightmap))
        {
            if (Hashset.Add(chunkCoord)) Queue.Add(chunkCoord);
            if (neighbours)
            {
                if (Hashset.Add(chunkCoord + new int2(+1, 0))) Queue.Add(chunkCoord + new int2(+1, 0));
                if (Hashset.Add(chunkCoord + new int2(-1, 0))) Queue.Add(chunkCoord + new int2(-1, 0));
                if (Hashset.Add(chunkCoord + new int2(0, +1))) Queue.Add(chunkCoord + new int2(0, +1));
                if (Hashset.Add(chunkCoord + new int2(0, -1))) Queue.Add(chunkCoord + new int2(0, -1));
            }

            height = default;
            normal = new float3(0f, 1f, 0f);
            return false;
        }

        int2 dataCoord = WorldToData(position - ChunkToWorld(chunkCoord));
        if (dataCoord.x < 0 || dataCoord.y < 0 || dataCoord.x >= NumVertsPerLine || dataCoord.y >= NumVertsPerLine)
        {
            throw new IndexOutOfRangeException($"{dataCoord.x} {dataCoord.y} (/ {NumVertsPerLine} {NumVertsPerLine})");
        }

        height = Sample(heightmap, position, chunkCoord, dataCoord, out normal);

        return true;
    }

    [BurstCompile]
    public static float Sample(in ReadOnlySpan<float> heightmap, float2 position, int2 chunkCoord, int2 dataCoord, out float3 normal)
    {
        normal = new float3(0f, 1f, 0f);

        float h00 = heightmap[dataCoord.x + dataCoord.y * NumVertsPerLine];

        float2 dataPos00 = ChunkToWorld(chunkCoord) + DataToWorld(dataCoord);

        //Debug.DrawLine(
        //    new UnityEngine.Vector3(position.x, 0f, position.y),
        //    new UnityEngine.Vector3(position.x, 100f, position.y),
        //    Color.white,
        //    100f,
        //    true
        //);

        //DebugEx.DrawPoint(new(dataPos00.x, h00, dataPos00.y), 0.2f, Color.magenta, 100f, false);

        if (dataCoord.x >= NumVertsPerLine - 1 || dataCoord.y >= NumVertsPerLine - 1)
        {
            return h00;
        }

        float h01 = heightmap[dataCoord.x + (dataCoord.y + 1) * NumVertsPerLine];
        float h10 = heightmap[dataCoord.x + 1 + dataCoord.y * NumVertsPerLine];
        float h11 = heightmap[dataCoord.x + 1 + (dataCoord.y + 1) * NumVertsPerLine];

        float2 dataPos11 = ChunkToWorld(chunkCoord) + DataToWorld(dataCoord + new int2(1, 1));

        //DebugEx.DrawPoint(new(dataPos11.x, h10, dataPos00.y), 0.2f, Color.red, 100f, false);
        //DebugEx.DrawPoint(new(dataPos00.x, h01, dataPos11.y), 0.2f, Color.blue, 100f, false);
        //DebugEx.DrawPoint(new(dataPos11.x, h11, dataPos11.y), 0.2f, Color.white, 100f, false);

        float dx = (position.x - dataPos00.x) / (dataPos11.x - dataPos00.x);
        float dz = (position.y - dataPos00.y) / (dataPos11.y - dataPos00.y);

        if (dz < dx)
        {
            float3 v0 = new(dataPos00.x, h00, dataPos00.y);
            float3 v1 = new(dataPos11.x, h10, dataPos00.y);
            float3 v2 = new(dataPos11.x, h11, dataPos11.y);

            //DebugEx.DrawTriangle(
            //    v0,
            //    v1,
            //    v2,
            //    Color.gray, 100f, false);

            normal = math.normalize(math.cross(v1 - v0, v2 - v0));
            return (1 - dx) * h00 + (dx - dz) * h10 + dz * h11;
        }
        else
        {
            float3 v0 = new(dataPos00.x, h00, dataPos00.y);
            float3 v1 = new(dataPos11.x, h11, dataPos11.y);
            float3 v2 = new(dataPos00.x, h01, dataPos11.y);

            //DebugEx.DrawTriangle(
            //    v0,
            //    v1,
            //    v2,
            //    Color.white, 100f, false);

            normal = math.normalize(math.cross(v1 - v0, v2 - v0));
            return (1 - dz) * h00 + dx * h11 + (dz - dx) * h01;
        }
    }
}

[BurstCompile(CompileSynchronously = true)]
partial struct TerrainGeneratorJobServer : IJobFor
{
    [ReadOnly] public NativeArray<int2>.ReadOnly Queue;
    [NativeDisableContainerSafetyRestriction] public NativeArray<NativeArray<float>>.ReadOnly Result;
    public NativeHeightMapSettings HeightMapSettings;

    [BurstCompile(CompileSynchronously = true)]
    public void Execute(int index)
    {
        int2 coord = Queue[index];
        float2 noiseOffset = new float2(coord.x, coord.y) * TerrainSystemServer.MeshWorldSize / TerrainSystemServer.meshScale;
        //Debug.Log(string.Format("Generating chunk at {0} {1}", coord.x, coord.y));
        HeightMapGenerator.GenerateHeightMap(Result[index].AsSpan(), TerrainSystemServer.NumVertsPerLine, TerrainSystemServer.NumVertsPerLine, HeightMapSettings.heightMultiplier, noiseOffset, in HeightMapSettings.noiseSettings);
    }
}
