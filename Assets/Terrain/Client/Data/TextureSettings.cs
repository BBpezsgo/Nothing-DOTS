using UnityEngine;

[CreateAssetMenu]
public class TextureSettings : ScriptableObject
{
    [Min(0)] public int Resolution = 256;
    public float HeightThreshold = 0.5f;
    public float Noise1Threshold = 0.5f;
    public float Noise2Threshold = 0.5f;
    public NoiseSettings NoiseSettings;

#if UNITY_EDITOR
    void OnValidate()
    {
        NoiseSettings.ValidateValues();
    }
#endif
}
