using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Transporter")]
public class TransporterAuthoring : MonoBehaviour
{
    [SerializeField] Transform? LoadPoint = default;

    class Baker : Baker<TransporterAuthoring>
    {
        public override void Bake(TransporterAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Transporter>(entity, new()
            {
                LoadPoint = authoring.LoadPoint != null ? authoring.LoadPoint.localPosition : default,
            });
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
            AddComponent<Vehicle>(entity);
        }
    }
}
