using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/UnitDatabase")]
public class UnitDatabaseAuthoring : MonoBehaviour
{
    [NotNull] public GameObject[]? Units = default;

    class Baker : Baker<UnitDatabaseAuthoring>
    {
        public override void Bake(UnitDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitDatabase());
            DynamicBuffer<BufferedUnit> units = AddBuffer<BufferedUnit>(entity);
            for (int i = 0; i < authoring.Units.Length; i++)
            {
                units.Add(new(GetEntity(authoring.Units[i], TransformUsageFlags.Dynamic), authoring.Units[i].name, 1f));
            }
        }
    }
}
