using Unity.Entities;
using UnityEngine;

#nullable enable

[AddComponentMenu("Authoring/Processor")]
public class ProcessorAuthoring : MonoBehaviour
{
    public string? SourceFile;

    class Baker : Baker<ProcessorAuthoring>
    {
        public override void Bake(ProcessorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Processor
            {
                SourceFile = authoring.SourceFile,
                SourceVersion = default,
                CompilerCache = Entity.Null,
                Registers = default,
            });
            AddBuffer<NativeExternalFunction>(entity);
        }
    }
}
