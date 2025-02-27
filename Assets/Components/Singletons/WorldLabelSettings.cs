using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

public class WorldLabelSettings : IComponentData
{
    [NotNull] public GameObject? Prefab = default;
}
