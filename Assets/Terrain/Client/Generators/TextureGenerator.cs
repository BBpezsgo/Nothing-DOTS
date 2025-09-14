using System;
using System.Threading;
using Unity.Profiling;
using UnityEngine;

static class TextureGenerator
{
    static readonly ProfilerMarker _marker = new("Terrain.TextureGenerator");

    public static Color32[] GenerateTexture(float[] heightMap, int heightMapSize, Vector2 offset, TextureSettings textureSettings)
    {
        using var _ = _marker.Auto();

        NoiseGenerator noiseGenerator1 = new(textureSettings.NoiseSettings, offset, 1);
        NoiseGenerator noiseGenerator2 = new(textureSettings.NoiseSettings, offset, 2);

        Color32[] result = new Color32[textureSettings.Resolution * textureSettings.Resolution];

        for (int y = 0; y < textureSettings.Resolution; y++)
        {
            for (int x = 0; x < textureSettings.Resolution; x++)
            {
                int hx = x * (heightMapSize - 1) / (textureSettings.Resolution - 1);
                int hy = y * (heightMapSize - 1) / (textureSettings.Resolution - 1);

                float noise1 = Math.Max(0f, noiseGenerator1.Sample(x, y) - textureSettings.Noise1Threshold) * (1f / textureSettings.Noise1Threshold);
                float noise2 = Math.Max(0f, noiseGenerator2.Sample(x, y) - textureSettings.Noise2Threshold) * (1f / textureSettings.Noise2Threshold);

                float snow = Math.Clamp((heightMap[hx + hy * heightMapSize] - textureSettings.HeightThreshold) * (1f / textureSettings.HeightThreshold), 0f, 1f);

                float grass1 = Math.Clamp(1f - (noise1 + noise2), 0f, 1f);
                float grass2 = Math.Clamp(noise1, 0f, 1f);
                float grass3 = Math.Clamp(noise2, 0f, 1f);

                grass1 = Math.Clamp(grass1 - snow, 0f, 1f);
                grass2 = Math.Clamp(grass2 - snow, 0f, 1f);
                grass3 = Math.Clamp(grass3 - snow, 0f, 1f);

                float total = grass1 + grass2 + grass3 + snow;

                if (total <= 1e-6f) continue;

                result[y * textureSettings.Resolution + x] = new Color(grass1, grass2, grass3, snow) / total;
            }
        }

        return result;
    }
}
