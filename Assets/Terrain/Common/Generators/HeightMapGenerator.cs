using System;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;

[BurstCompile]
public static class HeightMapGenerator
{
    static readonly ProfilerMarker _marker = new("Terrain.HeightMapGenerator");

    [BurstCompile]
    public static void GenerateHeightMap(in Span<float> values, int width, int height, float heightMultiplier, in NoiseGenerator noiseGenerator)
    {
        using var _ = _marker.Auto();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ref float v = ref values[x + y * width];
                v = noiseGenerator.Sample(x, y);
                v *= v * heightMultiplier;
            }
        }
    }

    [BurstCompile]
    public static void GenerateHeightMap(in Span<float> values, int width, int height, float heightMultiplier, float2 offset, in NoiseSettings noiseSettings)
    {
        NoiseGenerator noiseGenerator = new(noiseSettings, offset);
        GenerateHeightMap(values, width, height, heightMultiplier, in noiseGenerator);
    }

    public static float[] GenerateHeightMap(int width, int height, float heightMultiplier, float2 offset, in NoiseSettings noiseSettings)
    {
        float[] values = new float[width * height];
        NoiseGenerator noiseGenerator = new(noiseSettings, offset);
        GenerateHeightMap(values, width, height, heightMultiplier, in noiseGenerator);
        return values;
    }
}
