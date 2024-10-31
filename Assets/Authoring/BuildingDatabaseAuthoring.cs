using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct BufferedBuildingAuthoring
{
    [SerializeField] public GameObject Prefab;
    [SerializeField] public GameObject PlaceholderPrefab;
    [SerializeField] public float TotalProgress;
}

[AddComponentMenu("Authoring/BuildingDatabase")]
public class BuildingDatabaseAuthoring : MonoBehaviour
{
    [NotNull] public BufferedBuildingAuthoring[]? Buildings = default;

    class Baker : Baker<BuildingDatabaseAuthoring>
    {
        public override void Bake(BuildingDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuildingDatabase());
            DynamicBuffer<BufferedBuilding> buildings = AddBuffer<BufferedBuilding>(entity);
            for (int i = 0; i < authoring.Buildings.Length; i++)
            {
                BufferedBuildingAuthoring buildingAuthoring = authoring.Buildings[i];
                buildings.Add(new(
                    GetEntity(buildingAuthoring.Prefab, TransformUsageFlags.Dynamic),
                    GetEntity(buildingAuthoring.PlaceholderPrefab, TransformUsageFlags.Dynamic),
                    buildingAuthoring.Prefab.name,
                    buildingAuthoring.TotalProgress
                ));
            }
        }
    }
}
