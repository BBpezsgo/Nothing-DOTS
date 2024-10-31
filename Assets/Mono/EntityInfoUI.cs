using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class EntityInfoUI : MonoBehaviour
{
    [SerializeField, NotNull] public RectTransform? HealthBarTransform = default;
    [SerializeField, NotNull] public Image? Foreground = default;
    [SerializeField, NotNull] public CanvasGroup? CanvasGroup = default;
    [SerializeField, NotNull] public Image? SelectionStatusIndicator = default;

    [ReadOnly] public float Percent = default;
    [ReadOnly] public float3 WorldPosition = default;
    [ReadOnly] public SelectionStatus SelectionStatus = default;
}
