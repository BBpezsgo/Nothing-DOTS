using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct Extractor : IComponentData
{
    public const float ExtractRadius = 2f;
    public const float ExtractSpeed = 1f;

    [GhostField(Quantization = 100)] public float ExtractProgress;
    public float3 ExtractPoint;
}
