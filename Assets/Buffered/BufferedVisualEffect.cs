using Unity.Burst;
using Unity.Entities;
using UnityEngine.VFX;

[BurstCompile]
public struct BufferedVisualEffect : IBufferElementData
{
    public required UnityObjectRef<VisualEffectAsset> VisualEffect;
}
