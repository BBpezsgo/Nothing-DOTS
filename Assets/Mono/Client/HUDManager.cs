using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDManager : MonoBehaviour
{
    [SerializeField, NotNull] UIDocument? _ui = default;

    [NotNull] Label? _labelResources = default;
    [NotNull] Label? _labelFps = default;

    float _refreshAt = default;
    float _maxDeltaTime = default;

    void Start()
    {
        _labelResources = _ui.rootVisualElement.Q<Label>("label-resources");
        _labelFps = _ui.rootVisualElement.Q<Label>("label-fps");
    }

    void Update()
    {
        float now = Time.time;
        _maxDeltaTime = MathF.Max(_maxDeltaTime, Time.deltaTime);
        if (now < _refreshAt) return;
        _refreshAt = now + 1f;

        _labelFps.text = MathF.Abs(_maxDeltaTime) < 0.01f ? "-" : MathF.Round(1f / _maxDeltaTime).ToString();
        _maxDeltaTime = 0f;

        if (PlayerManager.TryGetLocalPlayer(out Player localPlayer))
        {
            _labelResources.text = localPlayer.Resources.ToString();
        }
    }
}
