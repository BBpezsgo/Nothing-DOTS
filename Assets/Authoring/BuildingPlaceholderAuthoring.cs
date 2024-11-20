using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/BuildingPlaceholder")]
public class BuildingPlaceholderAuthoring : MonoBehaviour
{
    class Baker : Baker<BuildingPlaceholderAuthoring>
    {
        public override void Bake(BuildingPlaceholderAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BuildingPlaceholder>(entity);
            AddComponent<UnitTeam>(entity);
        }
    }
}
