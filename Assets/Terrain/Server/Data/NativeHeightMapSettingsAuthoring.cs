using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Native Height Map Settings")]
public class NativeHeightMapSettingsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] HeightMapSettings? Settings = null;

    class Baker : Baker<NativeHeightMapSettingsAuthoring>
    {
        public override void Bake(NativeHeightMapSettingsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<NativeHeightMapSettings>(entity, new()
            {
                heightMultiplier = authoring.Settings.heightMultiplier,
                noiseSettings = authoring.Settings.noiseSettings,
            });
        }
    }
}
