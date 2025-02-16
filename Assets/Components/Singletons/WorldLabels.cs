using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

public class WorldLabels : IComponentData
{
    [NotNull] public GameObject? Prefab = default;
}
