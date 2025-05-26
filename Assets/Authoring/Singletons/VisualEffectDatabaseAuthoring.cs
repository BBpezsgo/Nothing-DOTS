using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Authoring/Visual Effect Database")]
public class VisualEffectDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] VisualEffectAsset[]? VisualEffects = default;

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
