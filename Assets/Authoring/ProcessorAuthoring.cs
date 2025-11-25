using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[AddComponentMenu("Authoring/Processor")]
class ProcessorAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? StatusLED = default;
    [SerializeField] GameObject? NetworkReceiveLED = default;
    [SerializeField] GameObject? NetworkSendLED = default;
    [SerializeField] GameObject? RadarLED = default;
    [SerializeField] GameObject? CustomLED = default;
    [SerializeField] GameObject? USBLED = default;
    [SerializeField] Transform? USBPosition = default;

    [SerializeField] string? Script = default;

    class Baker : Baker<ProcessorAuthoring>
    {
        public override void Bake(ProcessorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Processor>(entity, new()
            {
                StatusLED = new(authoring.StatusLED != null ? GetEntity(authoring.StatusLED, TransformUsageFlags.Dynamic) : Entity.Null),
                NetworkReceiveLED = new(authoring.NetworkReceiveLED != null ? GetEntity(authoring.NetworkReceiveLED, TransformUsageFlags.Dynamic) : Entity.Null),
                NetworkSendLED = new(authoring.NetworkSendLED != null ? GetEntity(authoring.NetworkSendLED, TransformUsageFlags.Dynamic) : Entity.Null),
                RadarLED = new(authoring.RadarLED != null ? GetEntity(authoring.RadarLED, TransformUsageFlags.Dynamic) : Entity.Null),
                CustomLED = new(authoring.CustomLED != null ? GetEntity(authoring.CustomLED, TransformUsageFlags.Dynamic) : Entity.Null),
                USBLED = new(authoring.USBLED != null ? GetEntity(authoring.USBLED, TransformUsageFlags.Dynamic) : Entity.Null),
                USBPosition = authoring.USBPosition != null ? authoring.transform.InverseTransformPoint(authoring.USBPosition.position) : float3.zero,
                USBRotation = authoring.USBPosition != null ? authoring.USBPosition.rotation : quaternion.identity,
#if UNITY_EDITOR
                SourceFile = authoring.Script is not null ? new FileId(authoring.Script, NetcodeEndPoint.Server) : default,
#endif
            });
        }
    }
}
