using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Authoring/Visual Effect Database")]
public class VisualEffectDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] public VisualEffectAsset[]? VisualEffects = default;

    public int Find(VisualEffectAsset? visualEffect)
    {
        if (visualEffect == null) return -1;
        for (int i = 0; i < VisualEffects.Length; i++)
        {
            if (VisualEffects[i] != visualEffect) continue;
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
            AddComponent(entity, new VisualEffectDatabase());
            DynamicBuffer<BufferedVisualEffect> visualEffects = AddBuffer<BufferedVisualEffect>(entity);
            foreach (var visualEffect in authoring.VisualEffects)
            {
                visualEffects.Add(new()
                {
                    VisualEffect = visualEffect,
                });
            }
        }
    }
}
