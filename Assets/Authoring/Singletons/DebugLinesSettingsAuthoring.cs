using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Debug Lines Settings")]
public class DebugLinesSettingsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] Material[]? Materials = default;

    class Baker : Baker<DebugLinesSettingsAuthoring>
    {
        public override void Bake(DebugLinesSettingsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<DebugLinesSettings>(entity, new()
            {
                Materials = authoring.Materials,
            });
        }
    }
}
