using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : Singleton<UIManager>
{
    [Header("Documents")]
    [SerializeField, NotNull] public UIDocument? Unit = default;
    [SerializeField, NotNull] public UIDocument? Factory = default;

    public IEnumerable<UIDocument> UIs
    {
        get
        {
            yield return Unit;
            yield return Factory;
        }
    }

    [NotNull] Dictionary<UIDocument, IUICleanup>? OpenedUIs = default;

    [Header("Debug")]
    [SerializeField, ReadOnly] bool _escPressed = false;
    [SerializeField, ReadOnly] bool _escGrabbed = false;

    void Start()
    {
        OpenedUIs = new();
    }

    void Update()
    {
        _escPressed = Input.GetKeyDown(KeyCode.Escape);
        _escGrabbed = _escGrabbed && _escPressed;
    }

    void LateUpdate()
    {
        if (GrapESC()) CloseAllUI();
    }

    public bool GrapESC()
    {
        if (_escGrabbed || !_escPressed) return false;
        _escGrabbed = true;
        return true;
    }

    public void CloseAllUI()
    {
        foreach (UIDocument ui in UIs)
        {
            CloseUI(ui);
        }
    }

    public void CloseUI(UIDocument ui)
    {
        if (OpenedUIs.TryGetValue(ui, out IUICleanup? cleanup))
        { cleanup.Cleanup(ui); }
        ui.rootVisualElement?.focusController.focusedElement?.Blur();
        ui.gameObject.SetActive(false);
    }

    public void OpenUI(UIDocument ui)
    {
        CloseAllUI();
        ui.gameObject.SetActive(true);
    }
}
