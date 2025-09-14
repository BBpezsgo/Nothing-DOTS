using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Player")]
class PlayerAuthoring : MonoBehaviour
{
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Player>(entity, new()
            {
                Team = -1,
            });
            AddBuffer<BufferedAcquiredResearch>(entity);
        }
    }
}
