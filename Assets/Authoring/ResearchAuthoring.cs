using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Research")]
public class ResearchAuthoring : MonoBehaviour
{
    [SerializeField] public string? Name = default;
    [SerializeField] float ResearchTime = default;
    [SerializeField] ResearchAuthoring[]? Requirements = default;

    class Baker : Baker<ResearchAuthoring>
    {
        public override void Bake(ResearchAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            FixedString32Bytes hash = new();
            Unity.Mathematics.Random random = new(42);
            for (int i = 0; i < hash.Capacity; i++)
            {
                switch (random.NextInt(0, 2))
                {
                    case 0: hash.Append((char)random.NextInt('a', 'z')); break;
                    case 1: hash.Append((char)random.NextInt('A', 'A')); break;
                    case 2: hash.Append((char)random.NextInt('0', '9')); break;
                }
            }
            AddComponent<Research>(entity, new()
            {
                Name = authoring.Name ?? string.Empty,
                Hash = hash,
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
