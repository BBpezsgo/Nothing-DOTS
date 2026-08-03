using UnityEngine;
using UnityEngine.UIElements;

class RegisterTooltips : MonoBehaviour
{
    void OnEnable()
    {
        Tooltips.Instance.Reregister(GetComponent<UIDocument>().rootVisualElement);
    }
}
