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
            AddComponent<Research>(entity, new()
            {
                Name = authoring.Name ?? string.Empty,
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