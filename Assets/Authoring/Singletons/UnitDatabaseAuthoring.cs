using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/UnitDatabase")]
class UnitDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] public AllPrefabs? Prefabs = default;

    class Baker : Baker<UnitDatabaseAuthoring>
    {
        public override void Bake(UnitDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UnitDatabase>(entity, new());
            DynamicBuffer<BufferedUnit> units = AddBuffer<BufferedUnit>(entity);
            foreach (UnitPrefab item in authoring.Prefabs.Units)
            {
                units.Add(new()
                {
                    Prefab = GetEntity(item.Prefab, TransformUsageFlags.Dynamic),
                    Name = item.Prefab.name,
                    ProductionTime = item.ProductionTime,
                    RequiredResources = item.RequiredResources,
                    RequiredResearch =
                        item.RequiredResearch != null
                        ? item.RequiredResearch.Name
                        : string.Empty,
                });
            }
        }
    }
}
