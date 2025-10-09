using System;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class EntityInfoUIBar
{
    [SerializeField, NotNull] public GameObject? Object = default;
    [SerializeField, NotNull] public Image? Foreground = default;
    [ReadOnly] public bool IsVisible;
}

public class EntityInfoUI : MonoBehaviour
{
    [Header("UI")]

    [SerializeField, NotNull] public EntityInfoUIBar? HealthBar = default;
    [SerializeField, NotNull] public EntityInfoUIBar? BuildingProgress = default;
    [SerializeField, NotNull] public EntityInfoUIBar? TransporterLoad = default;
    [SerializeField, NotNull] public EntityInfoUIBar? TransporterProgress = default;
    [SerializeField, NotNull] public EntityInfoUIBar? ExtractorProgress = default;
    [SerializeField, NotNull] public CanvasGroup? CanvasGroup = default;
    [SerializeField, NotNull] public Image? SelectionStatusIndicator = default;
    [SerializeField, NotNull] public Text? Label = default;

    [Header("Debug")]

    [ReadOnly] public float HealthPercent = default;
    [ReadOnly] public float BuildingProgressPercent = default;
    [ReadOnly] public float TransporterLoadPercent = default;
    [ReadOnly] public float TransporterProgressPercent = default;
    [ReadOnly] public float ExtractorProgressPercent = default;
    [ReadOnly] public float3 Position = default;
    [ReadOnly] public quaternion Rotation = default;
    [ReadOnly] public Bounds Bounds = default;
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
