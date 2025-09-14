using UnityEngine;

[System.Serializable]
public struct NoiseSettings
{
	public float scale;

	public int octaves;
	[Range(0, 1)] public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;

	public void Reset()
	{
		scale = 50;
		octaves = 6;
		persistance = .6f;
		lacunarity = 2;
	}

	public void ValidateValues()
	{
		scale = Mathf.Max(scale, 0.01f);
		octaves = Mathf.Max(octaves, 1);
		lacunarity = Mathf.Max(lacunarity, 1);
		persistance = Mathf.Clamp01(persistance);
	}
}
