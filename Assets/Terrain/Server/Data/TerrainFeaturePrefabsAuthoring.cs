using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Feature Prefabs")]
public class TerrainFeaturePrefabsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? ResourcePrefab = null;

    class Baker : Baker<TerrainFeaturePrefabsAuthoring>
    {
        public override void Bake(TerrainFeaturePrefabsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainFeaturePrefabs>(entity, new()
            {
                ResourcePrefab = GetEntity(authoring.ResourcePrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}
