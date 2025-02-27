using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/World Labels")]
public class WorldLabelsAuthoring : MonoBehaviour
{
    class Baker : Baker<WorldLabelsAuthoring>
    {
        public override void Bake(WorldLabelsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddBuffer<BufferedWorldLabel>(entity);
        }
    }
}
