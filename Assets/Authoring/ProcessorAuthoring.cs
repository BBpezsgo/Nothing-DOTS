using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Processor")]
public class ProcessorAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? StatusLED = default;
    [SerializeField] GameObject? NetworkReceiveLED = default;
    [SerializeField] GameObject? NetworkSendLED = default;
    [SerializeField] GameObject? RadarLED = default;

    class Baker : Baker<ProcessorAuthoring>
    {
        public override void Bake(ProcessorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Processor>(entity, new()
            {
                StatusLED = new(authoring.StatusLED == null ? Entity.Null : GetEntity(authoring.StatusLED, TransformUsageFlags.Dynamic)),
                NetworkReceiveLED = new(authoring.NetworkReceiveLED == null ? Entity.Null : GetEntity(authoring.NetworkReceiveLED, TransformUsageFlags.Dynamic)),
                NetworkSendLED = new(authoring.NetworkSendLED == null ? Entity.Null : GetEntity(authoring.NetworkSendLED, TransformUsageFlags.Dynamic)),
                RadarLED = new(authoring.RadarLED == null ? Entity.Null : GetEntity(authoring.RadarLED, TransformUsageFlags.Dynamic)),
            });
            AddBuffer<BufferedTransmittedUnitData>(entity);
            AddBuffer<BufferedInstruction>(entity);
        }
    }
}
