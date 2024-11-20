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
            if (authoring.gameObject.TryGetComponent<UnityEngine.Collider>(out UnityEngine.Collider? collider))
            {
                switch (collider)
                {
                    case UnityEngine.SphereCollider v:
                        {
                            AddComponent<Collider>(entity, new(new SphereCollider()
                            {
                                Radius = v.radius
                            }));
                            if (v.center != default)
                            {
                                Debug.LogWarning($"Offsetted colliders not supported");
                            }
                            break;
                        }
                    case UnityEngine.BoxCollider v:
                        {
                            AddComponent<Collider>(entity, new(new AABBCollider()
                            {
                                AABB = new()
                                {
                                    Center = v.center,
                                    Extents = v.size / 2f,
                                }
                            }));
                            if (v.center != default)
                            {
                                Debug.LogWarning($"Offsetted colliders not supported");
                            }
                            break;
                        }
                    default:
                        {
                            Debug.LogWarning($"Collider \"{collider.GetType().Name}\" not implemented", authoring.gameObject);
                            AddComponent<Collider>(entity, new(new SphereCollider()
                            {
                                Radius = 1f
                            }));
                            break;
                        }
                }
            }
            else
            {
                Debug.LogWarning($"No collider attached to gameobject", authoring.gameObject);
                AddComponent<Collider>(entity, new(new SphereCollider()
                {
                    Radius = 1f
                }));
            }
        }
    }
}
