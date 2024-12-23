using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/UI Prefabs")]
public class UIPrefabsAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? EntityInfo = default;

    class Baker : Baker<UIPrefabsAuthoring>
    {
        public override void Bake(UIPrefabsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<UIPrefabs>(entity, new()
            {
                EntityInfo = authoring.EntityInfo,
            });
        }
    }
}
