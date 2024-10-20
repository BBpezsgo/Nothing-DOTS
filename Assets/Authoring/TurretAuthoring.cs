using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/Turret")]
public class TurretAuthoring : MonoBehaviour
{
    [NotNull] public GameObject? CannonBallPrefab = default;
    [NotNull] public Transform? CannonBallSpawn = default;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                ProjectilePrefab = GetEntity(authoring.CannonBallPrefab, TransformUsageFlags.Dynamic),
                ShootPosition = GetEntity(authoring.CannonBallSpawn, TransformUsageFlags.Dynamic)
            });
        }
    }
}
