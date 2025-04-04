using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Rigidbody")]
public class RigidbodyAuthoring : MonoBehaviour
{
    class Baker : Baker<RigidbodyAuthoring>
    {
        public override void Bake(RigidbodyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Rigidbody>(entity, new()
            {
                IsEnabled = true,
            });
        }
    }
}
