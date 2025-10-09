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

    public const float cellSize = (NumVertsPerLine - 3) * meshScale / (NumVertsPerLine - 1);

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

    public static readonly float DataPointWorldSize = MeshWorldSize / (NumVertsPerLine - 3);

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
        TerrainFeaturePrefabs terrainFeatures = SystemAPI.GetSingleton<TerrainFeaturePrefabs>();

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
                results[i].AsReadOnly(),
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
        in NativeArray<float>.ReadOnly heightmap,
        in TerrainFeaturePrefabs terrainFeatures,
        ref EntityCommandBuffer commandBuffer)
    {
        ChunkToWorld(chunkCoord, out float2 chunkOrigin);
        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)math.abs(chunkCoord.x) + (uint)math.abs(chunkCoord.y));
        int n = random.NextInt(1, 8);
        for (int j = 0; j < n; j++)
        {
            int2 dataCoord = random.NextInt2(default, new int2(NumVertsPerLine - 1));
            DataToWorld(dataCoord, out float2 randomPosition);
            randomPosition += chunkOrigin + random.NextFloat2(new float2(-cellSize / 2, -cellSize / 2), new float2(cellSize / 2, cellSize / 2));
            float height = Sample(heightmap, randomPosition, chunkCoord, dataCoord);
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
    public static void ChunkToWorld(in int2 chunkCoord, out float2 worldPosition)
        => worldPosition = (float2)chunkCoord * MeshWorldSize;

    public static float2 ChunkToWorld(int2 chunkCoord)
        => (float2)chunkCoord * MeshWorldSize;

    [BurstCompile]
    public static void WorldToChunk(in float2 worldPosition, out int2 chunkCoord)
        => chunkCoord = (int2)math.round(worldPosition / MeshWorldSize);

    public static int2 WorldToChunk(float2 worldPosition)
        => (int2)math.round(worldPosition / MeshWorldSize);

    static readonly float2 _topLeft = new float2(-1, 1) * MeshWorldSize / 2f;

    /// <summary>
    /// Chunk relative world to data coord
    /// </summary>
    [BurstCompile]
    public static void WorldToData(in float2 worldPosition, out int2 dataCoord)
        => dataCoord = (int2)((worldPosition - _topLeft) * new float2(1f, -1f) / MeshWorldSize * (NumVertsPerLine - 3)) + new int2(1, 1);

    /// <summary>
    /// Chunk relative world to data coord
    /// </summary>
    public static int2 WorldToData(float2 worldPosition)
        => (int2)((worldPosition - _topLeft) * new float2(1f, -1f) / MeshWorldSize * (NumVertsPerLine - 3)) + new int2(1, 1);

    /// <summary>
    /// Data coord to chunk relative world
    /// </summary>
    [BurstCompile]
    public static void DataToWorld(in int2 dataCoord, out float2 worldPosition)
        => worldPosition = _topLeft + new float2(dataCoord.x - 1, dataCoord.y - 1) / (NumVertsPerLine - 3) * new float2(1f, -1f) * MeshWorldSize;

    /// <summary>
    /// Data coord to chunk relative world
    /// </summary>
    public static float2 DataToWorld(int2 dataCoord)
        => _topLeft + new float2(dataCoord.x - 1, dataCoord.y - 1) / (NumVertsPerLine - 3) * new float2(1f, -1f) * MeshWorldSize;

    [BurstCompile]
    public bool TrySample(in float2 position, out float height, bool neighbours = false)
    {
        WorldToChunk(position + new float2(DataPointWorldSize, DataPointWorldSize) * 0.5f, out int2 chunkCoord);

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
            return false;
        }

        ChunkToWorld(chunkCoord, out float2 chunkPosition);
        WorldToData(position - chunkPosition, out int2 dataCoord);
        if (dataCoord.x < 0 || dataCoord.y < 0 || dataCoord.x >= NumVertsPerLine || dataCoord.y >= NumVertsPerLine)
        {
            throw new IndexOutOfRangeException($"{dataCoord.x} {dataCoord.y} (/ {NumVertsPerLine} {NumVertsPerLine})");
        }

        height = Sample(heightmap, position, chunkCoord, dataCoord);

        return true;
    }

    [BurstCompile]
    public bool TrySample(in float2 position, out float height, out float3 normal, bool neighbours = false)
    {
        WorldToChunk(position + new float2(DataPointWorldSize, DataPointWorldSize) * 0.5f, out int2 chunkCoord);

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

        ChunkToWorld(chunkCoord, out float2 chunkPosition);
        WorldToData(position - chunkPosition, out int2 dataCoord);
        if (dataCoord.x < 0 || dataCoord.y < 0 || dataCoord.x >= NumVertsPerLine || dataCoord.y >= NumVertsPerLine)
        {
            throw new IndexOutOfRangeException($"{dataCoord.x} {dataCoord.y} (/ {NumVertsPerLine} {NumVertsPerLine})");
        }

        height = Sample(heightmap, position, chunkCoord, dataCoord, out normal);

        return true;
    }

    [BurstCompile]
    public bool TrySample(in float2 aabbMin, in float2 aabbMax, out float height, out float3 normal)
    {
        normal = default;
        height = default;

        if (!TrySample(new float2(aabbMin.x, aabbMin.y), out float h, out float3 n, false)) return false;
        height += h;
        normal += n;

        if (!TrySample(new float2(aabbMin.x, aabbMax.y), out h, out n, false)) return false;
        height += h;
        normal += n;

        if (!TrySample(new float2(aabbMax.x, aabbMin.y), out h, out n, false)) return false;
        height += h;
        normal += n;

        if (!TrySample(new float2(aabbMax.x, aabbMax.y), out h, out n, false)) return false;
        height += h;
        normal += n;

        height /= 4;
        normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
        if (normal.y < 0f) normal = -normal;

        return true;
    }

    [BurstCompile]
    public static float Sample(in NativeArray<float>.ReadOnly heightmap, in float2 position, in int2 chunkCoord, in int2 dataCoord)
    {
        float h00 = heightmap[dataCoord.x + dataCoord.y * NumVertsPerLine];

        ChunkToWorld(chunkCoord, out var chunkPosition);
        DataToWorld(dataCoord, out var dataPosition00);

        float2 dataPos00 = chunkPosition + dataPosition00;

        if (dataCoord.x >= NumVertsPerLine - 1 || dataCoord.y >= NumVertsPerLine - 1)
        {
            return h00;
        }

        float h11 = heightmap[dataCoord.x + 1 + (dataCoord.y + 1) * NumVertsPerLine];

        DataToWorld(dataCoord + new int2(1, 1), out var dataPosition11);

        float2 dataPos11 = chunkPosition + dataPosition11;

        float dx = (position.x - dataPos00.x) / (dataPos11.x - dataPos00.x);
        float dz = (position.y - dataPos00.y) / (dataPos11.y - dataPos00.y);

        if (dz < dx)
        {
            float h10 = heightmap[dataCoord.x + 1 + dataCoord.y * NumVertsPerLine];
            return (1 - dx) * h00 + (dx - dz) * h10 + dz * h11;
        }
        else
        {
            float h01 = heightmap[dataCoord.x + (dataCoord.y + 1) * NumVertsPerLine];
            return (1 - dz) * h00 + dx * h11 + (dz - dx) * h01;
        }
    }

    [BurstCompile]
    public static float Sample(in NativeArray<float>.ReadOnly heightmap, in float2 position, in int2 chunkCoord, in int2 dataCoord, out float3 normal)
    {
        normal = new float3(0f, 1f, 0f);

        float h00 = heightmap[dataCoord.x + dataCoord.y * NumVertsPerLine];

        ChunkToWorld(chunkCoord, out var chunkPosition);
        DataToWorld(dataCoord, out var dataPosition00);

        float2 dataPos00 = chunkPosition + dataPosition00;

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

        DataToWorld(dataCoord + new int2(1, 1), out float2 dataPosition11);

        float h01 = heightmap[dataCoord.x + (dataCoord.y + 1) * NumVertsPerLine];
        float h10 = heightmap[dataCoord.x + 1 + dataCoord.y * NumVertsPerLine];
        float h11 = heightmap[dataCoord.x + 1 + (dataCoord.y + 1) * NumVertsPerLine];

        float2 dataPos11 = chunkPosition + dataPosition11;

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

    public bool Raycast(in float3 origin, in float3 direction, float maxDistance, out float distance)
    {
        int gx = (int)math.floor(origin.x / cellSize);
        int gz = (int)math.floor(origin.z / cellSize);

        int stepX = direction.x > 0 ? 1 : -1;
        int stepZ = direction.z > 0 ? 1 : -1;

        float tDeltaX = math.abs(cellSize / direction.x);
        float tDeltaZ = math.abs(cellSize / direction.z);

        float nextBoundaryX = (gx + (stepX > 0 ? 1 : 0)) * cellSize;
        float nextBoundaryZ = (gz + (stepZ > 0 ? 1 : 0)) * cellSize;

        float tMaxX = (direction.x != 0)
            ? (nextBoundaryX - origin.x) / direction.x
            : float.PositiveInfinity;
        float tMaxZ = (direction.z != 0)
            ? (nextBoundaryZ - origin.z) / direction.z
            : float.PositiveInfinity;

        float traveled = 0f;

        while (traveled <= maxDistance)
        {
            float worldX = gx * cellSize;
            float worldZ = gz * cellSize;

            WorldToChunk(new float2(worldX + DataPointWorldSize * 0.5f, worldZ + DataPointWorldSize * 0.5f), out int2 chunkCoord);

            if (Heightmaps.TryGetValue(chunkCoord, out NativeArray<float>.ReadOnly heightmap))
            {
                ChunkToWorld(chunkCoord, out float2 chunkPosition);
                WorldToData(new float2(worldX, worldZ) - chunkPosition, out int2 dataCoord);
                float height = heightmap[dataCoord.x + dataCoord.y * NumVertsPerLine];

                if (origin.y + direction.y * traveled <= height)
                {
                    distance = traveled;
                    return true;
                }
            }

            if (tMaxX < tMaxZ)
            {
                traveled = tMaxX;
                tMaxX += tDeltaX;
                gx += stepX;
            }
            else
            {
                traveled = tMaxZ;
                tMaxZ += tDeltaZ;
                gz += stepZ;
            }
        }

        distance = default;
        return false;
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
        NativeArray<float> chunk = Result[index];
        HeightMapGenerator.GenerateHeightMap(ref chunk, TerrainSystemServer.NumVertsPerLine, TerrainSystemServer.NumVertsPerLine, HeightMapSettings.heightMultiplier, noiseOffset, in HeightMapSettings.noiseSettings, Allocator.Temp);
    }
}
