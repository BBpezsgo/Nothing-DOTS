using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Builder Turret")]
public class BuilderTurretAuthoring : MonoBehaviour
{
    public Transform? ShootPosition = default;

    class Baker : Baker<BuilderTurretAuthoring>
    {
        public override void Bake(BuilderTurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BuilderTurret
            {
                ShootPosition = authoring.ShootPosition == null ? Entity.Null : GetEntity(authoring.ShootPosition, TransformUsageFlags.Dynamic)
            });
        }
    }
}
