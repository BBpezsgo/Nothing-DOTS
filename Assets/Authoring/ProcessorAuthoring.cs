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
                StatusLED = new(
                    authoring.StatusLED == null
                    ? Entity.Null
                    : GetEntity(authoring.StatusLED, TransformUsageFlags.Dynamic)
                ),
                NetworkReceiveLED = new(
                    authoring.NetworkReceiveLED == null
                    ? Entity.Null
                    : GetEntity(authoring.NetworkReceiveLED, TransformUsageFlags.Dynamic)
                ),
                NetworkSendLED = new(
                    authoring.NetworkSendLED == null
                    ? Entity.Null
                    : GetEntity(authoring.NetworkSendLED, TransformUsageFlags.Dynamic)
                ),
                RadarLED = new(
                    authoring.RadarLED == null
                    ? Entity.Null
                    : GetEntity(authoring.RadarLED, TransformUsageFlags.Dynamic)
                ),
                USBLED = new(
                    authoring.USBLED == null
                    ? Entity.Null
                    : GetEntity(authoring.USBLED, TransformUsageFlags.Dynamic)
                ),
                USBPosition =
                    authoring.USBPosition == null
                    ? float3.zero
                    : authoring.transform.InverseTransformPoint(authoring.USBPosition.position),
                USBRotation =
                    authoring.USBPosition == null
                    ? quaternion.identity
                    : authoring.USBPosition.rotation,
#if UNITY_EDITOR
                SourceFile = authoring.Script is null ? default : new FileId(authoring.Script, NetcodeEndPoint.Server),
#endif
            });
        }
    }
}
