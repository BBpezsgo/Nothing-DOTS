using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Research")]
class ResearchAuthoring : MonoBehaviour
{
    [SerializeField] public string? Name = default;
    [SerializeField] float ResearchTime = default;
    [SerializeField] ResearchAuthoring[]? Requirements = default;

    class Baker : Baker<ResearchAuthoring>
    {
        public override unsafe void Bake(ResearchAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            byte* hash = stackalloc byte[30];
            Unity.Mathematics.Random random = new(42);
            for (int i = 0; i < 29; i++)
            {
                hash[i] = random.NextAlphanumeric();
            }
            hash[29] = 0;
            AddComponent<Research>(entity, new()
            {
                Name = authoring.Name ?? string.Empty,
                Hash = *(FixedBytes30*)hash,
                ResearchTime = authoring.ResearchTime,
            });
            DynamicBuffer<BufferedResearchRequirement> requirements = AddBuffer<BufferedResearchRequirement>(entity);
            if (authoring.Requirements is not null)
            {
                requirements.EnsureCapacity(authoring.Requirements.Length);
                foreach (ResearchAuthoring requirement in authoring.Requirements)
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
