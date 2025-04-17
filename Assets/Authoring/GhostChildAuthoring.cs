using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[AddComponentMenu("Authoring/GhostChild")]
public class GhostChildAuthoring : MonoBehaviour
{
    class Baker : Baker<GhostChildAuthoring>
    {
        public override void Bake(GhostChildAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<GhostChild>(entity, new()
            {
                ParentId = default,
                LocalParentId = default,
                LocalPosition = float3.zero,
                LocalRotation = quaternion.identity,
            });
        }
    }
}
