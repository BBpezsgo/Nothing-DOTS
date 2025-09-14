using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "Projectile", menuName = "Game/Projectile")]
class ProjectileStats : ScriptableObject
{
    [SerializeField, NotNull] public GameObject? Prefab = default;
    [SerializeField, Min(0f)] public float Damage = default;
    [SerializeField, Min(0f)] public float Speed = default;
    [SerializeField] public VisualEffectAsset? ImpactEffect = default;
}
