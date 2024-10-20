using Unity.Entities;
using UnityEngine;

#nullable enable

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
