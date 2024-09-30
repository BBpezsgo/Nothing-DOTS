using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
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
            AddComponent(entity, new URPMaterialPropertyBaseColor
            {
                Value = new float4(.3f, .3f, .3f, 1f)
            });
        }
    }
}
