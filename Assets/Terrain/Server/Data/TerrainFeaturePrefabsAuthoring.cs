using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Feature Prefabs")]
public class TerrainFeaturePrefabsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] public GameObject? ResourcePrefab = null;
    [SerializeField, NotNull] public GameObject? ObstaclePrefab = null;

    class Baker : Baker<TerrainFeaturePrefabsAuthoring>
    {
        public override void Bake(TerrainFeaturePrefabsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainFeaturePrefabs>(entity, new()
            {
                ResourcePrefab = GetEntity(authoring.ResourcePrefab, TransformUsageFlags.Dynamic),
                ObstaclePrefab = GetEntity(authoring.ObstaclePrefab, TransformUsageFlags.Dynamic),

                ResourcePrefabName = authoring.ResourcePrefab.name,
                ObstaclePrefabName = authoring.ObstaclePrefab.name,
            });
        }
    }
}
