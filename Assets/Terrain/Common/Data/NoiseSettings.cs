using System;
using UnityEngine;

[Serializable]
public struct NoiseSettings
{
    public float scale;

    public int octaves;
    [Range(0, 1)] public float persistance;
    public float lacunarity;

    [Min(1)] public uint seed;
    public Vector2 offset;

    public void Reset()
    {
        seed = 1;
        scale = 50;
        octaves = 6;
        persistance = .6f;
        lacunarity = 2;
    }

    public void ValidateValues()
    {
        seed = Math.Max(seed, 1);
        scale = Mathf.Max(scale, 0.01f);
        octaves = Mathf.Max(octaves, 1);
        lacunarity = Mathf.Max(lacunarity, 1);
        persistance = Mathf.Clamp01(persistance);
    }
}
