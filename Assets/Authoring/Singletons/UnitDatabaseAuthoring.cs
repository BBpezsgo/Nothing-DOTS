using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/UnitDatabase")]
public class UnitDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] Item[]? Units = default;

    [Serializable]
    class Item
    {
        [SerializeField, NotNull] public GameObject? Prefab = default;
        [Min(0f)]
        [SerializeField] public float ProductionTime = default;
        [Min(0f)]
        [SerializeField] public float RequiredResources = default;
        [SerializeField] public ResearchAuthoring? RequiredResearch = default;
    }

    class Baker : Baker<UnitDatabaseAuthoring>
    {
        public override void Bake(UnitDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitDatabase());
            DynamicBuffer<BufferedUnit> units = AddBuffer<BufferedUnit>(entity);
            foreach (Item item in authoring.Units)
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
