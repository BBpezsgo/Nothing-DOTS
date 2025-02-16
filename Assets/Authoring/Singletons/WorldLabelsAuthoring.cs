using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/World Labels")]
public class WorldLabelsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? _prefab = default;

    class Baker : Baker<WorldLabelsAuthoring>
    {
        public override void Bake(WorldLabelsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<WorldLabels>(entity, new()
            {
                Prefab = authoring._prefab,
            });
            AddBuffer<BufferedWorldLabel>(entity);
        }
    }
}
