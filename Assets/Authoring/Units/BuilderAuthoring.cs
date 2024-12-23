using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Builder")]
public class BuilderAuthoring : MonoBehaviour
{
    class Baker : Baker<BuilderAuthoring>
    {
        public override void Bake(BuilderAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Builder>(entity);
            AddComponent<SelectableUnit>(entity);
            AddComponent<EntityWithInfoUI>(entity);
            AddComponent<UnitTeam>(entity);
            AddComponent<Vehicle>(entity);
        }
    }
}
