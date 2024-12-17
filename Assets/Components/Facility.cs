using Unity.Entities;
using Unity.NetCode;

public struct Facility : IComponentData
{
    public const float ResearchSpeed = 1f;

    [GhostField] public BufferedResearch Current;
    [GhostField(Quantization = 100)] public float CurrentProgress;
}
