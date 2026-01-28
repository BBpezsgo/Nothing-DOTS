using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.VFX;

[BurstCompile]
public struct BufferedVisualEffect : IBufferElementData
{
    public required UnityObjectRef<VisualEffectAsset> VisualEffect;
    public required float3 LightColor;
    public required float LightRange;
    public required float LightIntensity;
    public required float Duration;
}
