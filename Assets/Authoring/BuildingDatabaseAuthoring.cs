using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/BuildingDatabase")]
public class BuildingDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] Item[]? Buildings = default;

    [Serializable]
    public class Item
    {
        [SerializeField, NotNull] public GameObject? Prefab = default;
        [SerializeField, NotNull] public GameObject? PlaceholderPrefab = default;
        [SerializeField] public float TotalProgress = default;
        [SerializeField] public ResearchAuthoring? RequiredResearch = default;
    }

    class Baker : Baker<BuildingDatabaseAuthoring>
    {
        public override void Bake(BuildingDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuildingDatabase());
            DynamicBuffer<BufferedBuilding> buildings = AddBuffer<BufferedBuilding>(entity);
            foreach (Item buildingAuthoring in authoring.Buildings)
            {
                buildings.Add(new()
                {
                    Name = buildingAuthoring.Prefab.name,
                    Prefab = GetEntity(buildingAuthoring.Prefab, TransformUsageFlags.Dynamic),
                    PlaceholderPrefab = GetEntity(buildingAuthoring.PlaceholderPrefab, TransformUsageFlags.Dynamic),
                    TotalProgress = buildingAuthoring.TotalProgress,
                    RequiredResearch =
                        buildingAuthoring.RequiredResearch != null
                        ? buildingAuthoring.RequiredResearch.Name
                        : string.Empty,
                });
            }
        }
    }
}
