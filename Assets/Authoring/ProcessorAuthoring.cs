using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Processor")]
public class ProcessorAuthoring : MonoBehaviour
{
    class Baker : Baker<ProcessorAuthoring>
    {
        public override void Bake(ProcessorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Processor
            {
                SourceFile = default,
                SourceVersion = default,
                CompilerCache = Entity.Null,
                Registers = default,
            });
            AddBuffer<NativeExternalFunction>(entity);
        }
    }
}
