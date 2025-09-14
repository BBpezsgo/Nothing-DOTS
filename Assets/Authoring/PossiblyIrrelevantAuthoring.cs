using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Possibly Irrelevant")]
class PossiblyIrrelevantAuthoring : MonoBehaviour
{
    [SerializeField, Min(0f)] float RelevancyRadius = 10f;

    class Baker : Baker<PossiblyIrrelevantAuthoring>
    {
        public override void Bake(PossiblyIrrelevantAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PossiblyIrrelevant>(entity, new()
            {
                RelevancyRadius = authoring.RelevancyRadius,
            });
        }
    }
}
