using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Builder Turret")]
public class BuilderTurretAuthoring : MonoBehaviour
{
    [SerializeField] Transform? Turret = default;

    [SerializeField] float TurretRotationSpeed = default;

    class Baker : Baker<BuilderTurretAuthoring>
    {
        public override void Bake(BuilderTurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BuilderTurret>(entity, new()
            {
                TurretRotationSpeed = authoring.TurretRotationSpeed,
                Turret = authoring.Turret != null ? GetEntity(authoring.Turret, TransformUsageFlags.Dynamic) : Entity.Null,
            });
        }
    }
}
