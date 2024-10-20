using UnityEngine;

#nullable enable

public static class MaterialExtensions
{
    public static void SetEmissionColor(this Material material, Color color, float emission)
    {
        material.color = color;

        if (material.HasColor("_Emission"))
        { material.SetColor("_Emission", color * emission); }

        if (material.HasColor("_EmissionColor"))
        { material.SetColor("_EmissionColor", color * emission); }
    }
}