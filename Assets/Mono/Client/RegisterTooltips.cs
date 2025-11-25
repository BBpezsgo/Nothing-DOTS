using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
class RegisterTooltips : MonoBehaviour
{
    void OnEnable()
    {
        Tooltips.Instance.Reregister(GetComponent<UIDocument>().rootVisualElement);
    }
}
