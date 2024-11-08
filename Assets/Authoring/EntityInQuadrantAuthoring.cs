using Unity.Entities;
using UnityEngine;

public class EntityInQuadrantAuthoring : MonoBehaviour
{
    class Baker : Baker<EntityInQuadrantAuthoring>
    {
        public override void Bake(EntityInQuadrantAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<QuadrantEntityIdentifier>(entity, new()
            {
                Layer = 1u << authoring.gameObject.layer,
            });
        }
    }
}
