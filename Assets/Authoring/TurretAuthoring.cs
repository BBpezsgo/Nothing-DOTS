using Unity.Entities;
using UnityEngine;

public class TurretAuthoring : MonoBehaviour
{
    public GameObject CannonBallPrefab;
    public Transform CannonBallSpawn;

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
