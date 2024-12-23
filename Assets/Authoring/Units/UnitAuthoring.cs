using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Unit")]
public class UnitAuthoring : MonoBehaviour
{
    class Baker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Unit>(entity, new());
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
            AddComponent<Vehicle>(entity);
        }
    }
}
