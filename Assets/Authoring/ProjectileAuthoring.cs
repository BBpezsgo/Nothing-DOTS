using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/Projectile")]
public class ProjectileAuthoring : MonoBehaviour
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
