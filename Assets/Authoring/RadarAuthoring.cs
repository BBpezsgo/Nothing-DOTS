using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Radar")]
public class RadarAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? Radar = default;

    class Baker : Baker<RadarAuthoring>
    {
        public override void Bake(RadarAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Radar()
            {
                Transform =
                    authoring.Radar == null
                    ? Entity.Null
                    : GetEntity(authoring.Radar, TransformUsageFlags.Dynamic),
            });
        }
    }
}
