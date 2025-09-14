using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct NativeHeightMapSettings : IComponentData
{
    public NoiseSettings noiseSettings;
    public float heightMultiplier;
}
