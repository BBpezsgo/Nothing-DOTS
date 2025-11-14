using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Extractor")]
public class ExtractorAuthoring : MonoBehaviour
{
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
        }
    }
}
