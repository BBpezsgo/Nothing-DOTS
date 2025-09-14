using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Terrain Unit")]
public class TerrainUnitAuthoring : MonoBehaviour
{
    class Baker : Baker<TerrainUnitAuthoring>
    {
        public override void Bake(TerrainUnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainUnit>(entity);
        }
    }
}
