using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using LanguageCore;
using System;
using NaughtyAttributes;
using BitMiracle.LibTiff.Classic;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using Unity.Profiling;
using Unity.Mathematics;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.NetCode;

public class TerrainGenerator : Singleton<TerrainGenerator>
{
    static readonly ProfilerMarker _marker1 = new("Terrain.TerrainGenerator.NewChunk");

    const float ViewerMoveThresholdForChunkUpdate = 25f;
    const float ViewerMoveThresholdForChunkUpdateSq = ViewerMoveThresholdForChunkUpdate * ViewerMoveThresholdForChunkUpdate;

    [NotNull] public float[]? DetailLevels = null;
    public float MaxViewDistance = 1000f;

    [NotNull] public HeightMapSettings? HeightMapSettings = null;
    [NotNull] public TextureSettings? TextureSettings = null;

    [NotNull] public Transform? Viewer = null;
    [NotNull] public Material? MapMaterial = null;

    float2 ViewerPosition;
    float2 ViewerPositionOld;

    float MeshWorldSize;
    [SerializeField, ReadOnly] int ChunksVisibleInViewDst;

    readonly Dictionary<int2, TerrainChunk> TerrainChunks = new();
    readonly List<TerrainChunk> VisibleTerrainChunks = new();

    void Start()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        TerrainChunks.Clear();
        VisibleTerrainChunks.Clear();

        if (World.DefaultGameObjectInjectionWorld.IsServer())
        {
            enabled = false;
            return;
        }

        MeshWorldSize = TerrainSystemServer.MeshWorldSize;
        ChunksVisibleInViewDst = Mathf.RoundToInt(MaxViewDistance / MeshWorldSize);

        UpdateVisibleChunks();
    }

    void Update()
    {
        ViewerPosition = new float2(Viewer.position.x, Viewer.position.z);

        if (math.lengthsq(ViewerPositionOld - ViewerPosition) > ViewerMoveThresholdForChunkUpdateSq)
        {
            ViewerPositionOld = ViewerPosition;
            UpdateVisibleChunks();
        }

        foreach (var item in TerrainChunks)
        {
            float2 p = TerrainSystemServer.ChunkToWorld(item.Value.Coord);
            for (int i = 0; i < item.Value.LodMeshes.Length; i++)
            {
                if (item.Value.LodMeshes[i].Mesh != null ||
                    item.Value.LodMeshes[i].Texture != null)
                {
                    DebugEx.DrawBoxAligned(new float3(p.x, item.Value.LodMeshes.Length - i, p.y), new float3(TerrainSystemServer.MeshWorldSize, 0f, TerrainSystemServer.MeshWorldSize), Color.green, 0f, false);
                }
            }
        }
    }

    void CreateChunk(int2 chunkCoord)
    {
        using var _ = _marker1.Auto();
        TerrainChunk newChunk = new(chunkCoord, HeightMapSettings, TextureSettings, DetailLevels, Viewer, MaxViewDistance, OnTerrainChunkVisibilityChanged);
        TerrainChunks.Add(chunkCoord, newChunk);
        newChunk.Load(transform, MapMaterial);
    }

    public void TryGenerate(int2 chunkCoord)
    {
        if (TerrainChunks.ContainsKey(chunkCoord)) return;
        CreateChunk(chunkCoord);
    }

    public bool TryGetHeightmap(int2 chunkCoord, bool queue, [NotNullWhen(true)] out float[]? heightMap)
    {
        heightMap = null;

        if (TerrainChunks.TryGetValue(chunkCoord, out TerrainChunk? chunk))
        {
            if (chunk.HeightMap is null) return false;

            heightMap = chunk.HeightMap;
            return true;
        }

        if (!queue) return false;

        CreateChunk(chunkCoord);

        return false;
    }

    public bool TrySample(float2 position, out float height, out float3 normal, bool neighbours = false)
    {
        int2 chunkCoord = TerrainSystemServer.WorldToChunk(position + new float2(TerrainSystemServer.DataPointWorldSize, TerrainSystemServer.DataPointWorldSize) * 0.5f);

        if (!TryGetHeightmap(chunkCoord, true, out float[]? heightmap))
        {
            if (neighbours)
            {
                TryGenerate(chunkCoord + new int2(+1, 0));
                TryGenerate(chunkCoord + new int2(-1, 0));
                TryGenerate(chunkCoord + new int2(0, +1));
                TryGenerate(chunkCoord + new int2(0, -1));
            }

            height = default;
            normal = new float3(0f, 1f, 0f);
            return false;
        }

        int2 dataCoord = TerrainSystemServer.WorldToData(position - TerrainSystemServer.ChunkToWorld(chunkCoord));
        if (dataCoord.x < 0 || dataCoord.y < 0 || dataCoord.x >= TerrainSystemServer.NumVertsPerLine || dataCoord.y >= TerrainSystemServer.NumVertsPerLine)
        {
            throw new IndexOutOfRangeException($"{dataCoord.x} {dataCoord.y} (/ {TerrainSystemServer.NumVertsPerLine} {TerrainSystemServer.NumVertsPerLine})");
        }

        height = TerrainSystemServer.Sample(heightmap, position, chunkCoord, dataCoord, out normal);

        //Debug.DrawRay(new float3(position.x, height, position.y), normal, Color.blue, 1000f);
        return true;
    }

    public bool TrySampleFast(float2 position, out float height, bool neighbours = false)
    {
        int2 chunkCoord = TerrainSystemServer.WorldToChunk(position + new float2(TerrainSystemServer.DataPointWorldSize * 0.5f, TerrainSystemServer.DataPointWorldSize * 0.5f));

        if (!TryGetHeightmap(chunkCoord, true, out float[]? heightmap))
        {
            if (neighbours)
            {
                TryGenerate(chunkCoord + new int2(+1, 0));
                TryGenerate(chunkCoord + new int2(-1, 0));
                TryGenerate(chunkCoord + new int2(0, +1));
                TryGenerate(chunkCoord + new int2(0, -1));
            }

            height = default;
            return false;
        }

        int2 dataCoord = TerrainSystemServer.WorldToData(position - TerrainSystemServer.ChunkToWorld(chunkCoord));
        if (dataCoord.x < 0 || dataCoord.y < 0 || dataCoord.x >= TerrainSystemServer.NumVertsPerLine || dataCoord.y >= TerrainSystemServer.NumVertsPerLine)
        {
            throw new IndexOutOfRangeException($"{dataCoord.x} {dataCoord.y} (/ {TerrainSystemServer.NumVertsPerLine} {TerrainSystemServer.NumVertsPerLine})");
        }

        height = heightmap[dataCoord.x + dataCoord.y * TerrainSystemServer.NumVertsPerLine];

        //Debug.DrawRay(new float3(position.x, height, position.y), new Vector3(0f, 1f, 0f), Color.blue, 1000f);
        return true;
    }

    public bool Raycast(float3 origin, float3 direction, float maxDistance, out float distance)
    {
        int gx = (int)MathF.Floor(origin.x / TerrainSystemServer.cellSize);
        int gz = (int)MathF.Floor(origin.z / TerrainSystemServer.cellSize);

        int stepX = direction.x > 0 ? 1 : -1;
        int stepZ = direction.z > 0 ? 1 : -1;

        float tDeltaX = MathF.Abs(TerrainSystemServer.cellSize / direction.x);
        float tDeltaZ = MathF.Abs(TerrainSystemServer.cellSize / direction.z);

        float nextBoundaryX = (gx + (stepX > 0 ? 1 : 0)) * TerrainSystemServer.cellSize;
        float nextBoundaryZ = (gz + (stepZ > 0 ? 1 : 0)) * TerrainSystemServer.cellSize;

        float tMaxX = (direction.x != 0)
            ? (nextBoundaryX - origin.x) / direction.x
            : float.PositiveInfinity;
        float tMaxZ = (direction.z != 0)
            ? (nextBoundaryZ - origin.z) / direction.z
            : float.PositiveInfinity;

        float traveled = 0f;

        while (traveled <= maxDistance)
        {
            float worldX = gx * TerrainSystemServer.cellSize;
            float worldZ = gz * TerrainSystemServer.cellSize;

            int2 chunkCoord = TerrainSystemServer.WorldToChunk(new float2(worldX + TerrainSystemServer.DataPointWorldSize * 0.5f, worldZ + TerrainSystemServer.DataPointWorldSize * 0.5f));

            if (TryGetHeightmap(chunkCoord, false, out float[]? heightmap))
            {
                int2 dataCoord = TerrainSystemServer.WorldToData(new float2(worldX, worldZ) - TerrainSystemServer.ChunkToWorld(chunkCoord));
                float height = heightmap[dataCoord.x + dataCoord.y * TerrainSystemServer.NumVertsPerLine];

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

    void UpdateVisibleChunks()
    {
        HashSet<int2> alreadyUpdatedChunkCoords = new();
        for (int i = VisibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(VisibleTerrainChunks[i].Coord);
            VisibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition.x / MeshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition.y / MeshWorldSize);

        for (int yOffset = -ChunksVisibleInViewDst; yOffset <= ChunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -ChunksVisibleInViewDst; xOffset <= ChunksVisibleInViewDst; xOffset++)
            {
                int2 viewedChunkCoord = new(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) continue;

                if (TerrainChunks.ContainsKey(viewedChunkCoord))
                {
                    TerrainChunks[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    CreateChunk(viewedChunkCoord);
                }
            }
        }
    }

    IEnumerator GenerateAsync()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
            if (i % 50 == 0) yield return null;
        }
        TerrainChunks.Clear();
        VisibleTerrainChunks.Clear();

        float maxViewDst = DetailLevels[^1];
        MeshWorldSize = TerrainSystemServer.MeshWorldSize;
        ChunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / MeshWorldSize);

        // Example coordinate (latitude, longitude)
        double latitude = 47.5;   // near Alps
        double longitude = 11.4;

        string latTile = $"{(latitude >= 0 ? "N" : "S")}{Math.Abs(Math.Floor(latitude)):00}_00";
        string lonTile = $"{(longitude >= 0 ? "E" : "W")}{Math.Abs(Math.Floor(longitude)):000}_00";

        string tileName = $"Copernicus_DSM_COG_10_{latTile}_{lonTile}_DEM";
        string url = $"https://copernicus-dem-30m.s3.amazonaws.com/{tileName}/{tileName}.tif";

        string outputPath = Path.Combine(Application.dataPath, "Resources", "HttpCache", Path.GetFileName(new Uri(url).LocalPath));

        if (!File.Exists(outputPath))
        {
            using HttpClient http = new();

            Debug.Log($"Downloading {url}");

            using HttpResponseMessage response = http.GetAsync(url).WaitForResult();
            response.EnsureSuccessStatusCode();

            if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            }

            using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            response.Content.CopyToAsync(fs).Wait();
        }

        /*
        AssetDatabase.Refresh();

        string resourcePath = $"HttpCache/{Path.GetFileNameWithoutExtension(new Uri(url).LocalPath)}";
        Texture2D resource = Resources.Load<Texture2D>(resourcePath);

        if (resource == null)
        {
            throw new Exception($"Failed to load image");
        }

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(resource));
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteImportMode = SpriteImportMode.None;

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.filterMode = FilterMode.Point;
        settings.textureType = TextureImporterType.Default;
        settings.readable = true;
        settings.mipmapEnabled = false;
        settings.textureType = TextureImporterType.SingleChannel;
        settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        importer.SetTextureSettings(settings);

        foreach (var item in new string[] { "Standalone", "Web", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" })
        {
            yield return null;
            var v = importer.GetPlatformTextureSettings(item);
            v.format = TextureImporterFormat.RFloat;
            importer.SetPlatformTextureSettings(v);
        }

        importer.SaveAndReimport();

        resource = Resources.Load<Texture2D>(resourcePath);
        */

        float[] data;
        int dataWidth;
        int dataHeight;

        yield return new WaitForSeconds(0.5f);

        using (Tiff tif = Tiff.Open(outputPath, "r"))
        {
            if (tif == null) throw new Exception("Could not open GeoTIFF");

            dataWidth = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            dataHeight = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int bitsPerSample = tif.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
            SampleFormat sampleFormat = (SampleFormat)tif.GetField(TiffTag.SAMPLEFORMAT)[0].ToInt();

            if (bitsPerSample != 32 || sampleFormat != SampleFormat.IEEEFP) throw new Exception("Expected 32-bit float GeoTIFF");

            data = new float[dataWidth * dataHeight];

            if (tif.IsTiled())
            {
                int tileWidth = tif.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                int tileHeight = tif.GetField(TiffTag.TILELENGTH)[0].ToInt();

                byte[] buffer = new byte[tif.TileSize()];

                for (int y = 0; y < dataHeight; y += tileHeight)
                {
                    for (int x = 0; x < dataWidth; x += tileWidth)
                    {
                        tif.ReadTile(buffer, 0, x, y, 0, 0);

                        int bufferIndex = 0;
                        for (int ty = 0; ty < tileHeight && (y + ty) < dataHeight; ty++)
                        {
                            for (int tx = 0; tx < tileWidth && (x + tx) < dataWidth; tx++)
                            {
                                float value = BitConverter.ToSingle(buffer, bufferIndex);
                                data[(x + tx) + (y + ty) * dataWidth] = value;
                                bufferIndex += 4;
                            }
                        }

                        yield return null;
                    }
                }
            }
            else
            {
                byte[] buffer = new byte[dataWidth * 4]; // 4 bytes per float

                for (int y = 0; y < dataHeight; y++)
                {
                    tif.ReadScanline(buffer, y);

                    for (int x = 0; x < dataWidth; x++)
                    {
                        data[x + y * dataWidth] = BitConverter.ToSingle(buffer, x * 4);
                    }

                    yield return null;
                }
            }
        }

        const int ScaleReciprocal = 2;

        int chunksH = dataHeight / TerrainSystemServer.NumVertsPerLine / ScaleReciprocal;
        int chunksW = dataWidth / TerrainSystemServer.NumVertsPerLine / ScaleReciprocal;

        for (int cy = 0; cy < chunksH; cy++)
        {
            for (int cx = 0; cx < chunksW; cx++)
            {
                int2 viewedChunkCoord = new(cx, cy);
                TerrainChunk newChunk = new(viewedChunkCoord, HeightMapSettings, TextureSettings, DetailLevels, Viewer, MaxViewDistance, OnTerrainChunkVisibilityChanged);
                TerrainChunks.Add(viewedChunkCoord, newChunk);

                float[] result = new float[TerrainSystemServer.NumVertsPerLine * TerrainSystemServer.NumVertsPerLine];

                for (int y = 0; y < TerrainSystemServer.NumVertsPerLine; y++)
                {
                    for (int x = 0; x < TerrainSystemServer.NumVertsPerLine; x++)
                    {
                        float v = 0f;
                        for (int dy = 0; dy < ScaleReciprocal; dy++)
                        {
                            for (int dx = 0; dx < ScaleReciprocal; dx++)
                            {
                                v += data[
                                    (x + (cx * TerrainSystemServer.NumVertsPerLine) + dx) +
                                    ((TerrainSystemServer.NumVertsPerLine - 1 - y) + (cy * TerrainSystemServer.NumVertsPerLine) + dy) * dataWidth
                                ];
                            }
                        }
                        v /= ScaleReciprocal;
                        v *= 0.1f;
                        result[x + y * TerrainSystemServer.NumVertsPerLine] = v;
                    }
                }

                newChunk.OnHeightMapReceived(result);
            }
        }

        ViewerPosition = new float2(Viewer.position.x, Viewer.position.z);

        if (math.lengthsq(ViewerPositionOld - ViewerPosition) > ViewerMoveThresholdForChunkUpdateSq)
        {
            ViewerPositionOld = ViewerPosition;
            UpdateVisibleChunks();
        }
    }

    [Button]
    void Generate() => EditorCoroutineUtility.StartCoroutine(GenerateAsync(), this);

    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible)
    {
        if (isVisible)
        {
            VisibleTerrainChunks.Add(chunk);
        }
        else
        {
            VisibleTerrainChunks.Remove(chunk);
        }
    }
}
