using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Authoring/Combat Turret")]
public class CombatTurretAuthoring : MonoBehaviour
{
    [SerializeField] Transform? Turret = default;
    [SerializeField] Transform? Cannon = default;
    [SerializeField] ProjectileStats? Projectile = default;
    [SerializeField] Transform? ShootPosition = default;
    [SerializeField] VisualEffectAsset? ShootEffect = default;

    [SerializeField, NaughtyAttributes.MinMaxSlider(-90f, 90f)] Vector2 AngleConstraint = new(-90f, 90f);

    [SerializeField] float TurretRotationSpeed = default;
    [SerializeField] float CannonRotationSpeed = default;

    class Baker : Baker<CombatTurretAuthoring>
    {
        public override void Bake(CombatTurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new CombatTurret
            {
                MinAngle = authoring.AngleConstraint.x * Mathf.Deg2Rad,
                MaxAngle = authoring.AngleConstraint.y * Mathf.Deg2Rad,
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
                Projectile = FindFirstObjectByType<ProjectileDatabaseAuthoring>().Find(authoring.Projectile),
                ShootPosition =
                    authoring.ShootPosition == null
                    ? Entity.Null
                    : GetEntity(authoring.ShootPosition, TransformUsageFlags.Dynamic),
                ShootEffect = FindFirstObjectByType<VisualEffectDatabaseAuthoring>().Find(authoring.ShootEffect),
            });
        }
    }
}
