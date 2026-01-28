using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Authoring/Visual Effect Database")]
public class VisualEffectDatabaseAuthoring : MonoBehaviour
{
    [Serializable]
    struct VisualEffectItem
    {
        [SerializeField, Min(0f)] public float Duration;
        [SerializeField, NotNull] public VisualEffectAsset? VisualEffect;
        [SerializeField] public Light? Light;
    }

    [SerializeField, NotNull] VisualEffectItem[]? VisualEffects = default;

    public int Find(VisualEffectAsset? visualEffect)
    {
        if (visualEffect == null) return -1;
        for (int i = 0; i < VisualEffects.Length; i++)
        {
            if (VisualEffects[i].VisualEffect != visualEffect) continue;
            return i;
        }
        Debug.LogError($"Visual effect {visualEffect} is not present in the database", visualEffect);
        return -1;
    }

    class Baker : Baker<VisualEffectDatabaseAuthoring>
    {
        public override void Bake(VisualEffectDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<VisualEffectDatabase>(entity, new());
            DynamicBuffer<BufferedVisualEffect> visualEffects = AddBuffer<BufferedVisualEffect>(entity);
            foreach (VisualEffectItem visualEffect in authoring.VisualEffects)
            {
                visualEffects.Add(new()
                {
                    Duration = visualEffect.Duration,
                    VisualEffect = visualEffect.VisualEffect,
                    LightColor = visualEffect.Light == null ? default : new(visualEffect.Light.color.r, visualEffect.Light.color.g, visualEffect.Light.color.b),
                    LightIntensity = visualEffect.Light == null ? default : visualEffect.Light.intensity,
                    LightRange = visualEffect.Light == null ? default : visualEffect.Light.range,
                });
            }
        }
    }
}
