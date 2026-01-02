using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Extractor")]
public class ExtractorAuthoring : MonoBehaviour
{
    [SerializeField] AllPrefabs? Prefabs;
    [SerializeField] UnitPrefab? Prefab;

    [SerializeField] Transform? ExtractPoint = default;

    class Baker : Baker<ExtractorAuthoring>
    {
        public override void Bake(ExtractorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Extractor>(entity, new()
            {
                ExtractPoint = authoring.ExtractPoint != null ? authoring.ExtractPoint.localPosition : default,
            });
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
            AddComponent<Vehicle>(entity);
            if (authoring.Prefab != null)
            {
                if (authoring.Prefabs == null)
                {
                    Debug.LogError($"Prefab is not null but the prefab database is", authoring);
                }
                else
                {
                    int index = authoring.Prefabs.Units.IndexOf(v => v == authoring.Prefab);
                    if (index == -1) Debug.LogError($"Invalid prefab instance", authoring);
                    AddComponent<UnitPrefabInstance>(entity, new()
                    {
                        Index = index
                    });
                }
            }
        }
    }
}
