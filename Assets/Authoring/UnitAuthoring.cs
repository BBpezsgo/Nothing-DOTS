using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Unit")]
public class UnitAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? Radar = default;

    class Baker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Unit()
            {
                Radar = authoring.Radar == null ? Entity.Null : GetEntity(authoring.Radar, TransformUsageFlags.Dynamic),
            });
            AddComponent<SelectableUnit>(entity);
        }
    }
}
