using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/BuildingDatabase")]
public class BuildingDatabaseAuthoring : MonoBehaviour
{
    [NotNull] public GameObject[]? Buildings = default;

    class Baker : Baker<BuildingDatabaseAuthoring>
    {
        public override void Bake(BuildingDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuildingDatabase());
            DynamicBuffer<BufferedBuilding> buildings = AddBuffer<BufferedBuilding>(entity);
            for (int i = 0; i < authoring.Buildings.Length; i++)
            {
                buildings.Add(new(GetEntity(authoring.Buildings[i], TransformUsageFlags.Dynamic), authoring.Buildings[i].name));
            }
        }
    }
}
