using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Turret")]
public class TurretAuthoring : MonoBehaviour
{
    [SerializeField] Transform? Cannon = default;
    [SerializeField] GameObject? ProjectilePrefab = default;
    [SerializeField] Transform? ShootPosition = default;

    [SerializeField] float TurretRotationSpeed = default;
    [SerializeField] float CannonRotationSpeed = default;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                TurretRotationSpeed = authoring.TurretRotationSpeed,
                CannonRotationSpeed = authoring.CannonRotationSpeed,
                Cannon =
                    authoring.Cannon == null
                    ? Entity.Null
                    : GetEntity(authoring.Cannon, TransformUsageFlags.Dynamic),
                ProjectilePrefab =
                    authoring.ProjectilePrefab == null
                    ? Entity.Null
                    : GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                ShootPosition =
                    authoring.ShootPosition == null
                    ? Entity.Null
                    : GetEntity(authoring.ShootPosition, TransformUsageFlags.Dynamic)
            });
        }
    }
}
