using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Building")]
public class TerrainBuildingAuthoring : MonoBehaviour
{
    class Baker : Baker<TerrainBuildingAuthoring>
    {
        public override void Bake(TerrainBuildingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainBuilding>(entity);
        }
    }
}
