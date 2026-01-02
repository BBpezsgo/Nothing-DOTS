using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Building")]
class BuildingAuthoring : MonoBehaviour
{
    [SerializeField] AllPrefabs? Prefabs;
    [SerializeField] BuildingPrefab? Prefab;
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
            if (authoring.Prefab != null)
            {
                if (authoring.Prefabs == null)
                {
                    Debug.LogError($"Prefab is not null but the prefab database is", authoring);
                }
                else
                {
                    int index = authoring.Prefabs.Buildings.IndexOf(v => v == authoring.Prefab);
                    if (index == -1) Debug.LogError($"Invalid prefab instance", authoring);
                    AddComponent<BuildingPrefabInstance>(entity, new()
                    {
                        Index = index
                    });
                }
            }
        }
    }
}
