using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Facility")]
class FacilityAuthoring : MonoBehaviour
{
    class Baker : Baker<FacilityAuthoring>
    {
        public override void Bake(FacilityAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Facility>(entity);
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddBuffer<BufferedResearch>(entity);
            AddBuffer<BufferedTechnologyHashIn>(entity);
            AddBuffer<BufferedTechnologyHashOut>(entity);
        }
    }
}
