using System;
using System.IO;
using BitMiracle.LibTiff.Classic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public class TerrainChunk
{
	readonly Action<TerrainChunk, bool> OnVisibilityChanged;
	public readonly int2 Coord;

	GameObject? MeshObject;
	readonly float2 NoiseSampleOffset;
	readonly float2 TextureSampleOffset;
	public readonly Bounds Bounds;

	MeshRenderer? MeshRenderer;
	MeshFilter? MeshFilter;

	readonly float[] DetailLevels;
	public readonly LODMesh[] LodMeshes;

	public float[]? HeightMap;
	int PreviousLODIndex1 = -1;
	//int PreviousLODIndex2 = -1;
	readonly float MaxViewDistanceSqr;

	readonly HeightMapSettings HeightMapSettings;
	readonly TextureSettings TextureSettings;
	readonly Transform Viewer;

	public TerrainChunk(int2 coord, HeightMapSettings heightMapSettings, TextureSettings textureSettings, float[] detailLevels, Transform viewer, float maxViewDistance, Action<TerrainChunk, bool> onVisibilityChanged)
	{
		Coord = coord;
		DetailLevels = detailLevels;
		HeightMapSettings = heightMapSettings;
		TextureSettings = textureSettings;
		Viewer = viewer;

		NoiseSampleOffset = (float2)Coord * TerrainSystemServer.MeshWorldSize / TerrainSystemServer.meshScale;
		TextureSampleOffset = Coord * textureSettings.Resolution;

		float2 position = (float2)Coord * TerrainSystemServer.MeshWorldSize;
		Bounds = new Bounds(new Vector3(position.x, 0f, position.y), new Vector3(TerrainSystemServer.MeshWorldSize, 0f, TerrainSystemServer.MeshWorldSize));

		LodMeshes = new LODMesh[detailLevels.Length];
		for (int i = 0; i < detailLevels.Length; i++)
		{
			LodMeshes[i] = new LODMesh(i, UpdateTerrainChunk);
		}

		MaxViewDistanceSqr = maxViewDistance * maxViewDistance;
		OnVisibilityChanged = onVisibilityChanged;
	}

	static (float[] Values, int Width, int Height) LoadDem(string path)
	{
		if (!File.Exists(path)) throw new FileNotFoundException($"DEM file not found: {path}");

		using Tiff tif = Tiff.Open(path, "r") ?? throw new Exception("Failed to open GeoTIFF");

		int width = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
		int height = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

		int[] raster = new int[height * width];
		if (!tif.ReadRGBAImage(width, height, raster)) throw new Exception("Could not read image");

		float[] data = new float[width * height];

		for (int i = 0; i < raster.Length; i++)
		{
			data[i] = (raster[i] & 0xff) % 255f;
		}

		/*
		int width = image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
		int height = image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
		int bitsPerSample = image.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
		int sampleFormat = image.GetField(TiffTag.SAMPLEFORMAT)[0].ToInt();

		if (bitsPerSample != 32 || sampleFormat != (int)SampleFormat.IEEEFP)
		{
			throw new Exception("Expected 32-bit float GeoTIFF (Copernicus DEM uses this)");
		}

		float[] data = new float[width * height];
		int scanlineSize = image.ScanlineSize();

		byte[] buffer = new byte[scanlineSize];

		for (int row = 0; row < height; row++)
		{
			image.ReadScanline(buffer, row);
			Buffer.BlockCopy(buffer, 0, data, row * width * sizeof(float), scanlineSize);
		}
		*/

		for (int i = 0; i < data.Length; i++)
		{
			if (data[i] != 0f)
			{
				Debug.Log(data[i]);
				break;
			}
		}

		return (data, width, height);
	}

	public void Load(Transform parent, Material material)
	{
		float2 position = (float2)Coord * TerrainSystemServer.MeshWorldSize;
		MeshObject = new GameObject("Chunk", typeof(MeshRenderer), typeof(MeshFilter));
		MeshRenderer = MeshObject.GetComponent<MeshRenderer>();
		MeshFilter = MeshObject.GetComponent<MeshFilter>();
		MeshRenderer.material = new Material(material);

		MeshObject.transform.position = new Vector3(position.x, 0, position.y);
		MeshObject.transform.parent = parent;
		MeshObject.SetActive(false);

		ThreadedDataRequester.RequestData(() =>
		{
			return HeightMapGenerator.GenerateHeightMap(TerrainSystemServer.NumVertsPerLine, TerrainSystemServer.NumVertsPerLine, HeightMapSettings.heightMultiplier, NoiseSampleOffset, in HeightMapSettings.noiseSettings);
		}, OnHeightMapReceived);
	}

	public void OnHeightMapReceived(float[] heightMapObject)
	{
		HeightMap = heightMapObject;

		UpdateTerrainChunk();
	}

	static readonly ProfilerMarker _markerBase = new("Terrain.TerrainChunk.UpdateTerrainChunk");
	static readonly ProfilerMarker _markerApplyMesh = new("Terrain.TerrainChunk.UpdateTerrainChunk.ApplyMesh");
	static readonly ProfilerMarker _markerRequestMesh = new("Terrain.TerrainChunk.UpdateTerrainChunk.RequestMesh");
	static readonly ProfilerMarker _markerApplyTexture = new("Terrain.TerrainChunk.UpdateTerrainChunk.ApplyTexture");
	static readonly ProfilerMarker _markerRequestTexture = new("Terrain.TerrainChunk.UpdateTerrainChunk.RequestTexture");

	public void UpdateTerrainChunk()
	{
		if (HeightMap is null) return;
		if (MeshObject == null) return;

		using var _ = _markerBase.Auto();

		float viewerDstFromNearestEdgeSqr = Bounds.SqrDistance(Viewer.position);

		bool wasVisible = MeshObject.activeSelf;
		bool visible = viewerDstFromNearestEdgeSqr <= MaxViewDistanceSqr;

		if (visible)
		{
			int lodIndex = 0;

			for (int i = 0; i < DetailLevels.Length - 1; i++)
			{
				if (viewerDstFromNearestEdgeSqr > DetailLevels[i] * DetailLevels[i])
				{
					lodIndex = i + 1;
				}
				else
				{
					break;
				}
			}

			if (lodIndex != PreviousLODIndex1)
			{
				LODMesh lodMesh = LodMeshes[lodIndex];

				if (lodMesh.Mesh != null)
				{
					using var _1 = _markerApplyMesh.Auto();
					PreviousLODIndex1 = lodIndex;
					MeshFilter!.mesh = lodMesh.Mesh;
				}
				else if (!lodMesh.HasRequestedMesh)
				{
					using var _1 = _markerRequestMesh.Auto();
					lodMesh.RequestMesh(HeightMap);
				}
			}

			//if (lodIndex != PreviousLODIndex2)
			//{
			//	LODMesh lodMesh = LodMeshes[lodIndex];
			//
			//	if (lodMesh.Texture != null)
			//	{
			//		using var _1 = _markerApplyTexture.Auto();
			//		PreviousLODIndex2 = lodIndex;
			//		MeshRenderer!.material.SetTexture("_MainTex", lodMesh.Texture);
			//	}
			//	else if (!lodMesh.HasRequestedTexture)
			//	{
			//		using var _1 = _markerRequestTexture.Auto();
			//		lodMesh.RequestTexture(HeightMap, TextureSettings, TextureSampleOffset);
			//	}
			//}
		}

		if (wasVisible != visible)
		{
			MeshObject.SetActive(visible);
			OnVisibilityChanged.Invoke(this, visible);
		}
	}
}

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

		Mesh.MeshDataArray meshDataArray = meshData.CreateNativeMesh(Unity.Collections.Allocator.Temp);
		meshData.Dispose();
		
		Mesh = new();
		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, Mesh);
		Mesh.RecalculateBounds();
		if (TerrainSystemServer.useFlatShading) Mesh.RecalculateNormals();

		//Mesh = meshData.CreateManagedMesh(Unity.Collections.Allocator.Temp);
		HasRequestedMesh = false;

		UpdateCallback();
	}

	public void RequestMesh(float[] heightMap)
	{
		HasRequestedMesh = true;
		ThreadedDataRequester.RequestData(() =>
		{
			return MeshGenerator.GenerateTerrainMesh(heightMap, TerrainSystemServer.NumVertsPerLine, LOD, Unity.Collections.Allocator.TempJob);
		}, OnMeshDataReceived);
	}

	static readonly ProfilerMarker _markerCreateTexture = new("Terrain.TerrainChunk.CreateTexture");
	void OnTextureReceived(Color32[] pixels)
	{
		using var _ = _markerCreateTexture.Auto();

		int size = (int)MathF.Sqrt(pixels.Length);
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

	public void RequestTexture(float[] heightMap, TextureSettings textureSettings, float2 offset)
	{
		HasRequestedTexture = true;
		ThreadedDataRequester.RequestData(() =>
		{
			return TextureGenerator.GenerateTexture(heightMap, TerrainSystemServer.NumVertsPerLine, offset, textureSettings);
		}, OnTextureReceived);
	}
}
