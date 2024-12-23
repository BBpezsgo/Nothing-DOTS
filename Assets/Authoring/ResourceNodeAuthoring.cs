using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Resource Node")]
public class ResourceNodeAuthoring : MonoBehaviour
{
    class Baker : Baker<ResourceNodeAuthoring>
    {
        public override void Bake(ResourceNodeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ResourceNode>(entity, new()
            {

            });
        }
    }
}
