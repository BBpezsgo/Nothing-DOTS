using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDManager : MonoBehaviour
{
    [SerializeField, NotNull] UIDocument? _ui = default;

    [NotNull] Label? _label = default;
    float _refreshAt = default;

    void Start()
    {
        _label = _ui.rootVisualElement.Q<Label>("label-resources");
    }

    void Update()
    {
        float now = Time.time;
        if (now < _refreshAt) return;
        _refreshAt = now + 2f;
        if (!PlayerManager.TryGetLocalPlayer(out Player localPlayer)) return;
        _label.text = localPlayer.Resources.ToString();
    }
}
