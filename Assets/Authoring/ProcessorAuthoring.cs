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
            AddComponent<Processor>(entity);
            AddBuffer<NativeExternalFunction>(entity);
            AddBuffer<BufferedTransmittedUnitData>(entity);
        }
    }
}
