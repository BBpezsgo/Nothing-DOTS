using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Features")]
public class TerrainFeaturesAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? ResourcePrefab = null;

    class Baker : Baker<TerrainFeaturesAuthoring>
    {
        public override void Bake(TerrainFeaturesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainFeatures>(entity, new()
            {
                ResourcePrefab = GetEntity(authoring.ResourcePrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}
