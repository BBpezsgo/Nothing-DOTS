using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Authoring/Projectile Database")]
public class ProjectileDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] VisualEffectDatabaseAuthoring? VisualEffects = default;
    [SerializeField, NotNull] Item[]? Projectiles = default;

    [Serializable]
    class Item
    {
        [SerializeField, NotNull] public GameObject? Prefab = default;
        [SerializeField, Min(0f)] public float Damage = default;
        [SerializeField, Min(0f)] public float Speed = default;
        [SerializeField] public VisualEffectAsset? ImpactEffect = default;
    }

    class Baker : Baker<ProjectileDatabaseAuthoring>
    {
        public override void Bake(ProjectileDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectileDatabase());
            DynamicBuffer<BufferedProjectile> projectiles = AddBuffer<BufferedProjectile>(entity);
            foreach (Item projectile in authoring.Projectiles)
            {
                projectiles.Add(new()
                {
                    Prefab = GetEntity(projectile.Prefab, TransformUsageFlags.Dynamic),
                    Damage = projectile.Damage,
                    Speed = projectile.Speed,
                    ImpactEffect = authoring.VisualEffects.Find(projectile.ImpactEffect),
                });
            }
        }
    }
}
