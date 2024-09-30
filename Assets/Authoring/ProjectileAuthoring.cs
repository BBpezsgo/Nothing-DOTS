using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ProjectileAuthoring : MonoBehaviour
{
    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Projectile>(entity);
            AddComponent<URPMaterialPropertyBaseColor>(entity);
        }
    }
}
