using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Building")]
class BuildingAuthoring : MonoBehaviour
{
    [SerializeField] int Team;

    class Baker : Baker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Building>(entity);
            AddComponent<UnitTeam>(entity, new()
            {
                Team = authoring.Team,
            });
        }
    }
}
