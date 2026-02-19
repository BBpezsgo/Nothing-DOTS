using System;
using SaintsField;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Feature Prefabs")]
public class TerrainFeaturePrefabsAuthoring : MonoBehaviour
{
    [Serializable]
    public class TerrainFeaturePrefabItem
    {
        [MinMaxSlider(0, 100)] public Vector2Int Quantity;
        public GameObject? Prefab;
    }

    [SerializeField] public TerrainFeaturePrefabItem[]? Prefabs;

    class Baker : Baker<TerrainFeaturePrefabsAuthoring>
    {
        public override void Bake(TerrainFeaturePrefabsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainFeaturePrefabs>(entity);
            DynamicBuffer<TerrainFeaturePrefab> buffer = AddBuffer<TerrainFeaturePrefab>(entity);
            foreach (TerrainFeaturePrefabItem item in authoring.Prefabs ?? Array.Empty<TerrainFeaturePrefabItem>())
            {
                if (item.Prefab == null) continue;
                buffer.Add(new TerrainFeaturePrefab()
                {
                    Quantity = new(item.Quantity.x, item.Quantity.y),
                    Prefab = GetEntity(item.Prefab, TransformUsageFlags.Dynamic),
                    PrefabName = item.Prefab.name,
                });
            }
        }
    }
}
