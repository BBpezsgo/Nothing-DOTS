using Unity.Entities;
using UnityEngine;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
[AddComponentMenu("Authoring/Fog Revealer")]
public class FogRevealerAuthoring : MonoBehaviour
{
    [SerializeField] int SightRange = default;
    [SerializeField] bool UpdateOnlyOnMove = default;

    class Baker : Baker<FogRevealerAuthoring>
    {
        public override void Bake(FogRevealerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<FogRevealer>(entity, new()
            {
                SightRange = authoring.SightRange,
                UpdateOnlyOnMove = authoring.UpdateOnlyOnMove,
            });
        }
    }
}
