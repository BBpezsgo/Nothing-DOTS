using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Unit")]
public class UnitAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? Radar = default;
    [SerializeField] GameObject? Turret = default;

    class Baker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Unit()
            {
                Radar = authoring.Radar == null ? Entity.Null : GetEntity(authoring.Radar, TransformUsageFlags.Dynamic),
                Turret = authoring.Turret == null ? Entity.Null : GetEntity(authoring.Turret, TransformUsageFlags.Dynamic),
            });
            AddComponent<SelectableUnit>(entity);
        }
    }
}
