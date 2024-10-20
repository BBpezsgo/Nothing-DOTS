using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/Factory")]
public class FactoryAuthoring : MonoBehaviour
{
    class Baker : Baker<FactoryAuthoring>
    {
        public override void Bake(FactoryAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Factory>(entity);
            AddComponent<SelectableUnit>(entity);
            AddBuffer<BufferedUnit>(entity);
        }
    }
}
