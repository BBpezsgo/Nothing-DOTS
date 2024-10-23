using NaughtyAttributes;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [SerializeField, ReadOnly] bool _escPressed = false;
    [SerializeField, ReadOnly] bool _escGrabbed = false;

    void Update()
    {
        _escPressed = Input.GetKeyDown(KeyCode.Escape);
        _escGrabbed = _escGrabbed && _escPressed;
    }

    public bool GrapESC()
    {
        if (_escGrabbed || !_escPressed) return false;
        _escGrabbed = true;
        return true;
    }

    public static void CloseAllPopupUI()
    {
        TerminalManager.Instance.CloseUI();
        FactoryManager.Instance.CloseUI();
    }
}
