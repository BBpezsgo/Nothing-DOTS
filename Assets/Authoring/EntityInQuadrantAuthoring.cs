using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Entity in Quadrant")]
public class EntityInQuadrantAuthoring : MonoBehaviour
{
    [SerializeField] bool IsStatic = default;

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
                            AddComponent<Collider>(entity, new(
                                authoring.IsStatic,
                                new SphereCollider()
                                {
                                    Radius = v.radius
                                }));
                            if (v.center != default)
                            {
                                Debug.LogWarning($"Offsetted sphere colliders not supported");
                            }
                            break;
                        }
                    case BoxCollider v:
                        {
                            AddComponent<Collider>(entity, new(
                                authoring.IsStatic,
                                new AABBCollider()
                                {
                                    AABB = new()
                                    {
                                        Center = v.center,
                                        Extents = v.size / 2f,
                                    }
                                }));
                            break;
                        }
                    default:
                        {
                            Debug.LogWarning($"Collider \"{collider.GetType().Name}\" not implemented", authoring.gameObject);
                            AddComponent<Collider>(entity, new(
                                authoring.IsStatic,
                                new SphereCollider()
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
                AddComponent<Collider>(entity, new(
                    authoring.IsStatic,
                    new SphereCollider()
                    {
                        Radius = 1f
                    }));
            }
        }
    }
}
