using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Turret")]
public class TurretAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? CannonBallPrefab = default;
    [SerializeField] Transform? CannonBallSpawn = default;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                ProjectilePrefab =
                    authoring.CannonBallPrefab == null
                    ? Entity.Null
                    : GetEntity(authoring.CannonBallPrefab, TransformUsageFlags.Dynamic),
                ShootPosition =
                    authoring.CannonBallSpawn == null
                    ? Entity.Null
                    : GetEntity(authoring.CannonBallSpawn, TransformUsageFlags.Dynamic)
            });
        }
    }
}
