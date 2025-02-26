using Unity.Entities;
using UnityEngine;

[AddComponentMenu("Authoring/UI Elements")]
public class UIElementsAuthoring : MonoBehaviour
{
    class Baker : Baker<UIElementsAuthoring>
    {
        public override void Bake(UIElementsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UIElements>(entity);
            AddBuffer<BufferedUIElement>(entity);
        }
    }
}
