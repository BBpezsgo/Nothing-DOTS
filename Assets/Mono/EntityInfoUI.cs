using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class EntityInfoUI : MonoBehaviour
{
    [Header("UI")]

    [SerializeField, NotNull] public RectTransform? HealthBarTransform = default;
    [SerializeField, NotNull] public Image? Foreground = default;
    [SerializeField, NotNull] public CanvasGroup? CanvasGroup = default;
    [SerializeField, NotNull] public Image? SelectionStatusIndicator = default;
    [SerializeField, NotNull] public Text? Label = default;

    [Header("Debug")]

    [ReadOnly] public float Percent = default;
    [ReadOnly] public float3 WorldPosition = default;
    [ReadOnly] public SelectionStatus SelectionStatus = default;

    [ReadOnly] public bool IsVisible = default;
    [ReadOnly] public int Team = default;

    void Start()
    {
        EntityInfoUIManager.Instance.UIs.Add(this);
    }

    void OnDestroy()
    {
        if (EntityInfoUIManager.InstanceOrNull == null) return;
        EntityInfoUIManager.InstanceOrNull.UIs.Remove(this);
    }
}
