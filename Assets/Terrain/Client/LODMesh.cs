using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public class LODMesh
{
    public Mesh? Mesh;
    public Texture2D? Texture;
    public bool HasRequestedMesh;
    public bool HasRequestedTexture;
    readonly int LOD;
    readonly Action UpdateCallback;

    public LODMesh(int lod, Action onUpdate)
    {
        LOD = lod;
        UpdateCallback = onUpdate;
    }

    static readonly ProfilerMarker _markerCreateMesh = new("Terrain.TerrainChunk.CreateMesh");
    void OnMeshDataReceived(MeshGenerator.MeshData meshData)
    {
        using var _ = _markerCreateMesh.Auto();

        Mesh.MeshDataArray meshDataArray = meshData.CreateNativeMesh(Allocator.Temp);
        meshData.Dispose();

        Mesh = new();
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, Mesh);
        Mesh.RecalculateBounds();
        if (TerrainSystemServer.useFlatShading) Mesh.RecalculateNormals();

        //Mesh = meshData.CreateManagedMesh(Unity.Collections.Allocator.Temp);
        HasRequestedMesh = false;

        UpdateCallback();
    }

    public void RequestMesh(NativeArray<float>.ReadOnly heightMap)
    {
        HasRequestedMesh = true;
        ThreadedDataRequester.RequestData(() =>
        {
            return MeshGenerator.GenerateTerrainMesh(heightMap, TerrainSystemServer.NumVertsPerLine, LOD, Allocator.TempJob);
        }, OnMeshDataReceived);
    }

    static readonly ProfilerMarker _markerCreateTexture = new("Terrain.TerrainChunk.CreateTexture");
    void OnTextureReceived(Color32[] pixels)
    {
        using var _ = _markerCreateTexture.Auto();

        int size = (int)math.sqrt(pixels.Length);
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        Texture = texture;
        HasRequestedTexture = false;
        UpdateCallback();
    }

    public void RequestTexture(NativeArray<float> heightMap, TextureSettings textureSettings, float2 offset)
    {
        HasRequestedTexture = true;
        ThreadedDataRequester.RequestData(() =>
        {
            return TextureGenerator.GenerateTexture(heightMap, TerrainSystemServer.NumVertsPerLine, offset, textureSettings);
        }, OnTextureReceived);
    }
}
