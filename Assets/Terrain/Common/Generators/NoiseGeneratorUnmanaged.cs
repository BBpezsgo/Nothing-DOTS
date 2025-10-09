using System;
using Unity.Collections;
using UnityEngine;

public readonly struct NoiseGeneratorUnmanaged : IDisposable
{
    readonly Unity.Mathematics.Random Random;
    readonly NativeArray<Vector2> OctaveOffsets;
    readonly float Scale;
    readonly float Persistance;
    readonly float Lacunarity;

    public NoiseGeneratorUnmanaged(uint seed, Vector2 offset, float scale = 1f, int octaves = 8, float persistance = 0.5f, float lacunarity = 2f, Allocator allocator = Allocator.Temp)
    {
        Random = new Unity.Mathematics.Random(seed);
        OctaveOffsets = new(octaves, allocator, NativeArrayOptions.UninitializedMemory);
        Scale = scale;
        Persistance = persistance;
        Lacunarity = lacunarity;

        for (int i = 0; i < OctaveOffsets.Length; i++)
        {
            OctaveOffsets[i] = new Vector2(Random.NextInt(-100000, 100000) + offset.x, Random.NextInt(-100000, 100000) - offset.y);
        }
    }

    public NoiseGeneratorUnmanaged(in NoiseSettings noiseSettings, Vector2 offset = default, uint seed = default, Allocator allocator = Allocator.Temp)
     : this(noiseSettings.seed + seed, noiseSettings.offset + offset, noiseSettings.scale, noiseSettings.octaves, noiseSettings.persistance, noiseSettings.lacunarity, allocator)
    { }

    public float Minimum
    {
        get
        {
            float amplitude = 1f;
            float frequency = 1f;
            float result = 0f;

            for (int i = 0; i < OctaveOffsets.Length; i++)
            {
                result -= amplitude;

                amplitude *= Persistance;
                frequency *= Lacunarity;
            }

            return result;
        }
    }

    public float Maximum
    {
        get
        {
            float amplitude = 1f;
            float frequency = 1f;
            float result = 0f;

            for (int i = 0; i < OctaveOffsets.Length; i++)
            {
                result += amplitude;

                amplitude *= Persistance;
                frequency *= Lacunarity;
            }

            return result;
        }
    }

    public float Sample(int x, int y)
    {
        float amplitude = 1;
        float frequency = 1;
        float noiseHeight = 0;

        for (int i = 0; i < OctaveOffsets.Length; i++)
        {
            float sampleX = (x + OctaveOffsets[i].x) / Scale * frequency;
            float sampleY = (y + OctaveOffsets[i].y) / Scale * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
            noiseHeight += perlinValue * amplitude;

            amplitude *= Persistance;
            frequency *= Lacunarity;
        }

        return noiseHeight;
    }

    public void Dispose()
    {
        OctaveOffsets.Dispose();
    }
}
