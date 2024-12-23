using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Builder")]
public class BuilderAuthoring : MonoBehaviour
{
    [SerializeField] GameObject? Turret = default;

    class Baker : Baker<BuilderAuthoring>
    {
        public override void Bake(BuilderAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Builder()
            {
                Turret = authoring.Turret == null ? Entity.Null : GetEntity(authoring.Turret, TransformUsageFlags.Dynamic),
            });
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
            AddComponent<Vehicle>(entity);
        }
    }
}
