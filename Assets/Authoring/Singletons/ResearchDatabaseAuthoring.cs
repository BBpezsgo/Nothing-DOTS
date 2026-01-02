using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Research Database")]
class ResearchDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] ResearchMetadata[]? Researches = default;

    class Baker : Baker<ResearchDatabaseAuthoring>
    {
        public override unsafe void Bake(ResearchDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ResearchDatabase>(entity);
            if (!IsBakingForEditor())
            {
                byte* hash = stackalloc byte[30];
                Unity.Mathematics.Random random = new(42);

                NativeArray<Entity> entities = new(authoring.Researches.Length, Allocator.Temp);
                CreateAdditionalEntities(entities, TransformUsageFlags.None);

                for (int i = 0; i < entities.Length; i++)
                {
                    ResearchMetadata item = authoring.Researches[i];

                    random.NextNonce(hash, 29);
                    hash[29] = 0;

                    AddComponent<Research>(entities[i], new()
                    {
                        Name = item.Name ?? string.Empty,
                        Hash = *(FixedBytes30*)hash,
                        ResearchTime = item.ResearchTime,
                    });

                    DynamicBuffer<BufferedResearchRequirement> requirements = AddBuffer<BufferedResearchRequirement>(entities[i]);
                    if (item.Requirements is not null)
                    {
                        requirements.EnsureCapacity(item.Requirements.Length);
                        foreach (ResearchMetadata requirement in item.Requirements)
                        {
                            requirements.Add(new BufferedResearchRequirement()
                            {
                                Name = requirement.Name,
                            });
                        }
                    }
                }
            }
        }
    }
}
