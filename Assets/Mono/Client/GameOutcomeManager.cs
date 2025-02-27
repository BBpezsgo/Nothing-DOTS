using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements;

public class GameOutcomeManager : MonoBehaviour
{
    [SerializeField, NotNull] UIDocument? _ui = default;

    [NotNull] Label? _labelOutcome = default;
    float _refreshAt = default;

    void Update()
    {
        float now = Time.time;
        if (now < _refreshAt) return;
        _refreshAt = now + 1f;

        if (!PlayerManager.TryGetLocalPlayer(out Player localPlayer)) return;

        if (_ui.enabled = localPlayer.Outcome != GameOutcome.None && !UIManager.Instance.AnyUIVisible)
        {
            _labelOutcome = _ui.rootVisualElement.Q<Label>("label-outcome");
            _labelOutcome.text = localPlayer.Outcome.ToString();
            _refreshAt = now + 0.1f;
        }
    }
}
