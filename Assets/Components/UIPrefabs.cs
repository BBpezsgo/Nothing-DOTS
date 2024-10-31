using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;

public class UIPrefabs : IComponentData
{
    [NotNull] public GameObject? EntityInfo = default;
}
