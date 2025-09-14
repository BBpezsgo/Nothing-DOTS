using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Projectile")]
class ProjectileAuthoring : MonoBehaviour
{
    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Projectile>(entity);
        }
    }
}
