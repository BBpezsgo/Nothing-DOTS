using Unity.Entities;
using UnityEngine;

public class ProcessorAuthoring : MonoBehaviour
{
    public string SourceFile;

    class Baker : Baker<ProcessorAuthoring>
    {
        public override void Bake(ProcessorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new Processor
            {
                SourceFile = authoring.SourceFile,
                CompileSecuedued = !string.IsNullOrEmpty(authoring.SourceFile),
            });
        }
    }
}
