using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Generic Unit")]
public class GenericUnitAuthoring : MonoBehaviour
{
    class Baker : Baker<GenericUnitAuthoring>
    {
        public override void Bake(GenericUnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SelectableUnit>(entity);
        }
    }
}
