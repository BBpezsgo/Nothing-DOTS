using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Connector")]
class ConnectorAuthoring : MonoBehaviour
{
    [SerializeField] Transform ConnectorPosition;

    class Baker : Baker<ConnectorAuthoring>
    {
        public override void Bake(ConnectorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Connector>(entity, new()
            {
                ConnectorPosition = authoring.transform.InverseTransformPoint(authoring.ConnectorPosition.position),
            });
            AddBuffer<BufferedWire>(entity);
        }
    }
}
