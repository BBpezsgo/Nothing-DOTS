using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Building")]
public class BuildingAuthoring : MonoBehaviour
{
    class Baker : Baker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Building>(entity);
        }
    }
}
