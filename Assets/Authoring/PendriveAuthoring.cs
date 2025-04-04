using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Pendrive")]
public class PendriveAuthoring : MonoBehaviour
{
    class Baker : Baker<PendriveAuthoring>
    {
        public override void Bake(PendriveAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Pendrive>(entity, new()
            {

            });
        }
    }
}
