using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Debug Lines")]
public class DebugLinesAuthoring : MonoBehaviour
{
    class Baker : Baker<DebugLinesAuthoring>
    {
        public override void Bake(DebugLinesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<DebugLines>(entity);
            AddBuffer<BufferedLine>(entity);
        }
    }
}
