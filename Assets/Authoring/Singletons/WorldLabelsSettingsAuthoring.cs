using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/World Label Settings")]
public class WorldLabelSettingsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? _prefab = default;

    class Baker : Baker<WorldLabelSettingsAuthoring>
    {
        public override void Bake(WorldLabelSettingsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<WorldLabelSettings>(entity, new()
            {
                Prefab = authoring._prefab,
            });
        }
    }
}
