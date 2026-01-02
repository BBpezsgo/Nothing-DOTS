using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/Save Transform")]
class SaveTransformAuthoring : MonoBehaviour
{
    class Baker : Baker<SaveTransformAuthoring>
    {
        public override void Bake(SaveTransformAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SaveTransform>(entity);
        }
    }
}
