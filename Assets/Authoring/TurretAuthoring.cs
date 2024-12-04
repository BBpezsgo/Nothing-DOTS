using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Turret")]
public class TurretAuthoring : MonoBehaviour
{
    public TurretType Type = default;
    public GameObject? CannonBallPrefab = default;
    public Transform? CannonBallSpawn = default;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                Type = authoring.Type,
                ProjectilePrefab = authoring.CannonBallPrefab == null ? Entity.Null : GetEntity(authoring.CannonBallPrefab, TransformUsageFlags.Dynamic),
                ShootPosition = authoring.CannonBallSpawn == null ? Entity.Null : GetEntity(authoring.CannonBallSpawn, TransformUsageFlags.Dynamic)
            });
        }
    }
}
