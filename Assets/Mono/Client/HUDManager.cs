using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDManager : Singleton<HUDManager>
{
    [SerializeField, NotNull] UIDocument? _ui = default;

    [NotNull] internal Label? _labelResources = default;
    [NotNull] internal Label? _labelFps = default;
    [NotNull] internal Label? _labelSelectedUnits = default;

    float _refreshAt = default;
    float _maxDeltaTime = default;

    void Start()
    {
        _labelResources = _ui.rootVisualElement.Q<Label>("label-resources");
        _labelFps = _ui.rootVisualElement.Q<Label>("label-fps");
        _labelSelectedUnits = _ui.rootVisualElement.Q<Label>("label-selected-units");
    }

    void Update()
    {
        float now = Time.time;
        _maxDeltaTime = MathF.Max(_maxDeltaTime, Time.deltaTime);
        if (now < _refreshAt) return;
        _refreshAt = now + 1f;

        _labelFps.text = MathF.Abs(_maxDeltaTime) < 0.01f ? "-" : MathF.Round(1f / _maxDeltaTime).ToString();
        _maxDeltaTime = 0f;

        if (PlayerSystemClient.TryGetLocalPlayer(out Player localPlayer))
        {
            _labelResources.text = localPlayer.Resources.ToString();
        }
    }
}
