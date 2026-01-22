using Unity.Entities;
using UnityEditor;
using UnityEngine;

[AddComponentMenu("Authoring/Save Prespawned Entity")]
class SavePrespawnedEntityAuthoring : MonoBehaviour
{
    class Baker : Baker<SavePrespawnedEntityAuthoring>
    {
        public override void Bake(SavePrespawnedEntityAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SavePrespawnedEntity>(entity, new()
            {
                Id = GlobalObjectId.GetGlobalObjectIdSlow(authoring.gameObject).ToString(),
            });
        }
    }
}
