using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Resource")]
public class ResourceAuthoring : MonoBehaviour
{
    class Baker : Baker<ResourceAuthoring>
    {
        public override void Bake(ResourceAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Resource>(entity, new()
            {
                InitialScale = authoring.transform.localScale.magnitude,
            });
        }
    }
}
