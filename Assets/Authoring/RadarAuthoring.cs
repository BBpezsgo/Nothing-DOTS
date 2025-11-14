using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Radar")]
class RadarAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? Radar = default;

    class Baker : Baker<RadarAuthoring>
    {
        public override void Bake(RadarAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Radar>(entity, new()
            {
                Transform = authoring.Radar != null ? GetEntity(authoring.Radar, TransformUsageFlags.Dynamic) : Entity.Null,
            });
        }
    }
}
