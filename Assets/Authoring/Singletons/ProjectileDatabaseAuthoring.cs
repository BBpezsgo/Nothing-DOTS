using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Projectile Database")]
public class ProjectileDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] Item[]? Projectiles = default;

    [Serializable]
    class Item
    {
        [SerializeField, NotNull] public GameObject? Prefab = default;
        [Min(0f)]
        [SerializeField] public float Damage = default;
        [Min(0f)]
        [SerializeField] public float Speed = default;
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
                });
            }
        }
    }
}
