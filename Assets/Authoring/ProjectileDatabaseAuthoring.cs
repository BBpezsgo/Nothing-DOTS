using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/ProjectileDatabase")]
public class ProjectileDatabaseAuthoring : MonoBehaviour
{
    [NotNull] public GameObject[]? Projectiles = default;

    class Baker : Baker<ProjectileDatabaseAuthoring>
    {
        public override void Bake(ProjectileDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectileDatabase());
            DynamicBuffer<BufferedProjectile> projectiles = AddBuffer<BufferedProjectile>(entity);
            for (int i = 0; i < authoring.Projectiles.Length; i++)
            {
                projectiles.Add(new(GetEntity(authoring.Projectiles[i], TransformUsageFlags.Dynamic)));
            }
        }
    }
}
