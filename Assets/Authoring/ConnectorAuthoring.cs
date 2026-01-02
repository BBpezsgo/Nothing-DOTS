using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Connector")]
class ConnectorAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] Transform? ConnectorPosition = default;

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
