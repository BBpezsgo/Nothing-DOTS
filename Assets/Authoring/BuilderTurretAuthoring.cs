using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Builder Turret")]
public class BuilderTurretAuthoring : MonoBehaviour
{
    class Baker : Baker<BuilderTurretAuthoring>
    {
        public override void Bake(BuilderTurretAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BuilderTurret>(entity, new()
            {
                
            });
        }
    }
}
