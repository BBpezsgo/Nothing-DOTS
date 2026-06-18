using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.Serialization;
using UnityEngine;

[AddComponentMenu("Authoring/ResearchAuthoring")]
class ResearchAuthoring : MonoBehaviour
{
    [DontSerialize, NotNull] public ResearchMetadata? Metadata = null;

    class Baker : Baker<ResearchAuthoring>
    {
        public override unsafe void Bake(ResearchAuthoring authoring)
        {
            byte* hash = stackalloc byte[30];
            Unity.Mathematics.Random random = new(42);

            random.NextNonce(hash, 29);
            hash[29] = 0;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<Research>(entity, new()
            {
                Name = authoring.Metadata.Name ?? string.Empty,
                Hash = *(FixedBytes30*)hash,
                ResearchTime = authoring.Metadata.ResearchTime,
            });

            DynamicBuffer<BufferedResearchRequirement> requirements = AddBuffer<BufferedResearchRequirement>(entity);
            if (authoring.Metadata.Requirements is not null)
            {
                requirements.EnsureCapacity(authoring.Metadata.Requirements.Length);
                foreach (ResearchMetadata requirement in authoring.Metadata.Requirements)
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
