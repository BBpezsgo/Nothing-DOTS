using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Rigidbody")]
public class RigidbodyAuthoring : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float Bounciness = 0.6f;

    class Baker : Baker<RigidbodyAuthoring>
    {
        public override void Bake(RigidbodyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Rigidbody>(entity, new()
            {
                IsEnabled = true,
                Bounciness = authoring.Bounciness,
            });
        }
    }
}
