using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Building Database")]
class BuildingDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] AllPrefabs? Prefabs = default;

    class Baker : Baker<BuildingDatabaseAuthoring>
    {
        public override void Bake(BuildingDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BuildingDatabase>(entity, new());
            DynamicBuffer<BufferedBuilding> buildings = AddBuffer<BufferedBuilding>(entity);
            foreach (BuildingPrefab buildingAuthoring in authoring.Prefabs.Buildings)
            {
                buildings.Add(new()
                {
                    Name = buildingAuthoring.Prefab.name,
                    Prefab = GetEntity(buildingAuthoring.Prefab, TransformUsageFlags.Dynamic),
                    PlaceholderPrefab = GetEntity(buildingAuthoring.PlaceholderPrefab, TransformUsageFlags.Dynamic),
                    ConstructionTime = buildingAuthoring.ConstructionTime,
                    RequiredResources = buildingAuthoring.RequiredResources,
                    RequiredResearch =
                        buildingAuthoring.RequiredResearch != null
                        ? buildingAuthoring.RequiredResearch.Name
                        : string.Empty,
                });
            }
        }
    }
}
