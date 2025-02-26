using Unity.Entities;
using UnityEngine;

public class ProcessorGUIManager : MonoBehaviour
{
    Texture2D? _1px;
    GUIStyle? _labelStyle;

    void OnGUI()
    {
        if (ConnectionManager.ClientOrDefaultWorld == null) return;

        using EntityQuery uiElementsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(UIElements), typeof(BufferedUIElement));
        if (!uiElementsQ.TryGetSingletonBuffer(out DynamicBuffer<BufferedUIElement> uiElements, true)) return;

        if (_1px == null)
        {
            _1px = new Texture2D(1, 1);
            _1px.SetPixel(0, 0, default);
        }

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
            };
            _labelStyle.normal.background = _1px;
        }

        for (int i = 0; i < uiElements.Length; i++)
        {
            BufferedUIElement uiElement = uiElements[i];
            switch (uiElement.Type)
            {
                case BufferedUIElementType.Label:
                    _1px.SetPixel(0, 0, default);
                    _labelStyle.normal.textColor = new Color(uiElement.Color.x, uiElement.Color.y, uiElement.Color.z);
                    GUI.Label(new Rect(
                        new Vector2(uiElement.Position.x, uiElement.Position.y),
                        new Vector2(uiElement.Size.x, uiElement.Size.y)
                    ), uiElement.Text.ToString(), _labelStyle);
                    break;
            }
        }
    }
}