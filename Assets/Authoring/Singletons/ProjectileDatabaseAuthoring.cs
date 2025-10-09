using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Projectile Database")]
class ProjectileDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] ProjectileStats[]? Projectiles = default;

    public int Find(ProjectileStats? projectile)
    {
        if (projectile == null) return -1;
        for (int i = 0; i < Projectiles.Length; i++)
        {
            if (Projectiles[i] != projectile) continue;
            return i;
        }
        Debug.LogError($"Projectile {projectile} is not present in the database", projectile);
        return -1;
    }

    class Baker : Baker<ProjectileDatabaseAuthoring>
    {
        public override void Bake(ProjectileDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectileDatabase());
            DynamicBuffer<BufferedProjectile> projectiles = AddBuffer<BufferedProjectile>(entity);
            foreach (ProjectileStats projectile in authoring.Projectiles)
            {
                projectiles.Add(new()
                {
                    Prefab = GetEntity(projectile.Prefab, TransformUsageFlags.Dynamic),
                    Damage = projectile.Damage,
                    Speed = projectile.Speed,
                    MetalImpactEffect = FindFirstObjectByType<VisualEffectDatabaseAuthoring>().Find(projectile.MetalImpactEffect),
                    DustImpactEffect = FindFirstObjectByType<VisualEffectDatabaseAuthoring>().Find(projectile.DustImpactEffect),
                });
            }
        }
    }
}
