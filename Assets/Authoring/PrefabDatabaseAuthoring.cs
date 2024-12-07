using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/PrefabDatabase")]
public class PrefabDatabaseAuthoring : MonoBehaviour
{
    [NotNull] public GameObject? PlayerPrefab = default;
    [NotNull] public GameObject? CoreComputerPrefab = default;
    [NotNull] public GameObject? Builder = default;

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