using Unity.Entities;
using UnityEngine;

public class DebugLinesSettings : IComponentData
{
    public Material[] Materials;

    public DebugLinesSettings()
    {
        Materials = null!;
    }

    public DebugLinesSettings(Material[] materials)
    {
        Materials = materials;
    }
}
