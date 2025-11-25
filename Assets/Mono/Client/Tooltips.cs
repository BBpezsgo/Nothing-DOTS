using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements;

public class Tooltips : Singleton<Tooltips>
{
    [NotNull] UIDocument? ui = null;
    [NotNull] Label? label = null;
    bool visible;

    void Start()
    {
        ui = GetComponent<UIDocument>();
        label = ui.rootVisualElement.Q<Label>("tooltip");
    }

    void Update()
    {
        if (!visible) return;

        label.style.left = Math.Max(0, Input.mousePosition.x);
        label.style.bottom = Math.Max(0, Input.mousePosition.y);
        label.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    public void Reregister(VisualElement? visualElement)
    {
        if (visualElement is null) return;

        if (visualElement.tooltip is not null)
        {
            visualElement.UnregisterCallback<MouseEnterEvent>(OnElementMouseEnter);
            visualElement.UnregisterCallback<MouseLeaveEvent>(OnElementMouseLeave);
            visualElement.RegisterCallback<MouseEnterEvent>(OnElementMouseEnter);
            visualElement.RegisterCallback<MouseLeaveEvent>(OnElementMouseLeave);
        }

        foreach (VisualElement item in visualElement.Children())
        {
            Reregister(item);
        }
    }

    public void Register(VisualElement? visualElement)
    {
        if (visualElement is null) return;

        if (visualElement.tooltip is not null)
        {
            visualElement.RegisterCallback<MouseEnterEvent>(OnElementMouseEnter);
            visualElement.RegisterCallback<MouseLeaveEvent>(OnElementMouseLeave);
        }

        foreach (VisualElement item in visualElement.Children())
        {
            Register(item);
        }
    }

    public void Unregister(VisualElement? visualElement)
    {
        if (visualElement is null) return;

        if (visualElement.tooltip is not null)
        {
            visualElement.UnregisterCallback<MouseEnterEvent>(OnElementMouseEnter);
            visualElement.UnregisterCallback<MouseLeaveEvent>(OnElementMouseLeave);
        }

        foreach (VisualElement item in visualElement.Children())
        {
            Unregister(item);
        }
    }

    void SetTooltip(string? tooltip)
    {
        visible = !string.IsNullOrWhiteSpace(tooltip);
        label.text = tooltip;
    }

    void OnElementMouseEnter(MouseEnterEvent e)
    {
        if (e.currentTarget is not VisualElement t) return;
        SetTooltip(t.tooltip);
    }

    void OnElementMouseLeave(MouseLeaveEvent e)
    {
        if (e.currentTarget is not VisualElement t) return;
        SetTooltip(null);
    }
}
