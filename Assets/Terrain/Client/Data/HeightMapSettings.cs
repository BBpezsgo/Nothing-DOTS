using UnityEngine;

[CreateAssetMenu]
public class HeightMapSettings : ScriptableObject
{
	public NoiseSettings noiseSettings;
	public float heightMultiplier;

#if UNITY_EDITOR
	void OnValidate()
	{
		noiseSettings.ValidateValues();
	}
#endif
}
