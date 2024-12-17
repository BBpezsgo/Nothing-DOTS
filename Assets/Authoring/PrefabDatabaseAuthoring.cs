using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Prefab Database")]
public class PrefabDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] GameObject? PlayerPrefab = default;
    [SerializeField, NotNull] GameObject? CoreComputerPrefab = default;
    [SerializeField, NotNull] GameObject? Builder = default;

    class Baker : Baker<PrefabDatabaseAuthoring>
    {
        public override void Bake(PrefabDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PrefabDatabase>(entity, new()
            {
                Player = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                CoreComputer = GetEntity(authoring.CoreComputerPrefab, TransformUsageFlags.Dynamic),
                Builder = GetEntity(authoring.Builder, TransformUsageFlags.Dynamic),
            });
        }
    }
}