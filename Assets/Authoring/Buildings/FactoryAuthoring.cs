using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Factory")]
class FactoryAuthoring : MonoBehaviour
{
    class Baker : Baker<FactoryAuthoring>
    {
        public override void Bake(FactoryAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Factory>(entity);
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddBuffer<BufferedProducingUnit>(entity);
        }
    }
}
