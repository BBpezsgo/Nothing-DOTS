using Unity.Entities;
using UnityEngine;

public class DamageableAuthoring : MonoBehaviour
{
    [SerializeField] float MaxHealth = default;

    class Baker : Baker<DamageableAuthoring>
    {
        public override void Bake(DamageableAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Damageable>(entity, new()
            {
                MaxHealth = authoring.MaxHealth,
                Health = authoring.MaxHealth,
            });
            AddBuffer<BufferedDamage>(entity);
        }
    }
}
