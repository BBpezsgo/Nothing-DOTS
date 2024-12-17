using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Projectile Database")]
public class ProjectileDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject[]? Projectiles = default;

    class Baker : Baker<ProjectileDatabaseAuthoring>
    {
        public override void Bake(ProjectileDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectileDatabase());
            DynamicBuffer<BufferedProjectile> projectiles = AddBuffer<BufferedProjectile>(entity);
            foreach (GameObject projectile in authoring.Projectiles)
            {
                projectiles.Add(new(GetEntity(projectile, TransformUsageFlags.Dynamic)));
            }
        }
    }
}
