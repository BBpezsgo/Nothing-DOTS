using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : Singleton<UIManager>
{
    public readonly struct UISetup
    {
        readonly UIDocument _ui;
        readonly UIManager _manager;

        public UISetup(UIDocument ui, UIManager manager)
        {
            _ui = ui;
            _manager = manager;
        }

        public UISetup Setup<TUI, TContext>(TUI ui, TContext context)
            where TUI : IUISetup<TContext>, IUICleanup
        {
            ui.Setup(_ui, context);
            _manager.OpenedUIs.TryAdd(_ui, new List<IUICleanup>());
            _manager.OpenedUIs[_ui].Add(ui);
            return this;
        }

        public UISetup Setup<TUI>(TUI ui)
            where TUI : IUISetup, IUICleanup
        {
            ui.Setup(_ui);
            _manager.OpenedUIs.TryAdd(_ui, new List<IUICleanup>());
            _manager.OpenedUIs[_ui].Add(ui);
            return this;
        }
    }

    [Header("Documents")]

    [SerializeField, NotNull] public UIDocument? Unit = default;
    [SerializeField, NotNull] public UIDocument? Factory = default;
    [SerializeField, NotNull] public UIDocument? Pause = default;

    public IEnumerable<UIDocument> UIs
    {
        get
        {
            yield return Unit;
            yield return Factory;
            yield return Pause;
        }
    }

    [NotNull] Dictionary<UIDocument, List<IUICleanup>>? OpenedUIs = default;

    public bool AnyUIVisible => UIs.Any(v => v.gameObject.activeSelf);

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

    // void LateUpdate()
    // {
    //     if (GrapESC()) CloseAllUI();
    // }

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
        if (OpenedUIs.TryGetValue(ui, out List<IUICleanup>? cleanup))
        {
            foreach (IUICleanup item in cleanup) item.Cleanup(ui);
            OpenedUIs.Remove(ui);
        }
        ui.rootVisualElement?.focusController.focusedElement?.Blur();
        ui.gameObject.SetActive(false);
    }

    public void CloseUI(IUICleanup ui)
    {
        foreach (KeyValuePair<UIDocument, List<IUICleanup>> item in OpenedUIs.ToArray())
        {
            if (!item.Value.Contains(ui)) continue;
            CloseUI(item.Key);
        }
    }

    public UISetup OpenUI(UIDocument ui)
    {
        CloseAllUI();
        ui.gameObject.SetActive(true);
        return new UISetup(ui, this);
    }
}
