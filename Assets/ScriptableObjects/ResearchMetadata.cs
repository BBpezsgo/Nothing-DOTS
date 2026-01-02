using System.Diagnostics.CodeAnalysis;
using UnityEngine;

[CreateAssetMenu(fileName = "Research", menuName = "Game/Research")]
class ResearchMetadata : ScriptableObject
{
    [SerializeField, NotNull] public string? Name = default;
    [SerializeField] public float ResearchTime = default;
    [SerializeField, NotNull] public ResearchMetadata[]? Requirements = default;
}
