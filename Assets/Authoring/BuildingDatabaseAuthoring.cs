using Unity.Entities;
using UnityEngine;

public class BuildingDatabaseAuthoring : MonoBehaviour
{
    public GameObject[] Buildings;

    class Baker : Baker<BuildingDatabaseAuthoring>
    {
        public override void Bake(BuildingDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuildingDatabase());
            DynamicBuffer<BufferedEntityPrefab> buildings = AddBuffer<BufferedEntityPrefab>(entity);
            for (int i = 0; i < authoring.Buildings.Length; i++)
            {
                buildings.Add(GetEntity(authoring.Buildings[i], TransformUsageFlags.Dynamic));
            }
        }
    }
}
