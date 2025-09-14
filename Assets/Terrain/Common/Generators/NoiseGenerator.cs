using UnityEngine;

public readonly struct NoiseGenerator
{
    readonly System.Random Random;
    readonly Vector2[] OctaveOffsets;
    readonly float Scale;
    readonly float Persistance;
    readonly float Lacunarity;

    public NoiseGenerator(int seed, Vector2 offset, float scale = 1f, int octaves = 8, float persistance = 0.5f, float lacunarity = 2f)
    {
        Random = new System.Random(seed);
        OctaveOffsets = new Vector2[octaves];
        Scale = scale;
        Persistance = persistance;
        Lacunarity = lacunarity;

        for (int i = 0; i < OctaveOffsets.Length; i++)
        {
            OctaveOffsets[i] = new Vector2(Random.Next(-100000, 100000) + offset.x, Random.Next(-100000, 100000) - offset.y);
        }
    }

    public NoiseGenerator(in NoiseSettings noiseSettings, Vector2 offset = default, int seed = default)
     : this(noiseSettings.seed + seed, noiseSettings.offset + offset, noiseSettings.scale, noiseSettings.octaves, noiseSettings.persistance, noiseSettings.lacunarity)
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
}
