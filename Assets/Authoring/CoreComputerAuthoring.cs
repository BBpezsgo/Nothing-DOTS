using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Core Computer")]
public class CoreComputerAuthoring : MonoBehaviour
{
    class Baker : Baker<CoreComputerAuthoring>
    {
        public override void Bake(CoreComputerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
        }
    }
}