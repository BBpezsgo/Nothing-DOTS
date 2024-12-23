using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Combat Turret")]
public class CombatTurretAuthoring : MonoBehaviour
{
    [SerializeField] Transform? Turret = default;
    [SerializeField] Transform? Cannon = default;
    [SerializeField] GameObject? ProjectilePrefab = default;
    [SerializeField] Transform? ShootPosition = default;

    [SerializeField] float TurretRotationSpeed = default;
    [SerializeField] float CannonRotationSpeed = default;

    class Baker : Baker<CombatTurretAuthoring>
    {
        public override void Bake(CombatTurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new CombatTurret
            {
                TurretRotationSpeed = authoring.TurretRotationSpeed,
                CannonRotationSpeed = authoring.CannonRotationSpeed,
                Turret =
                    authoring.Turret == null
                    ? Entity.Null
                    : GetEntity(authoring.Turret, TransformUsageFlags.Dynamic),
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
