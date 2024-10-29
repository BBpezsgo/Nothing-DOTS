using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public static class UIExtensions
{
    public static void SyncList<T>(
        this VisualElement container,
        DynamicBuffer<T> collection,
        VisualTreeAsset itemAsset,
        Action<T, VisualElement, bool> updater)
        where T : unmanaged
    {
        VisualElement[] childrenElement = container.Children().ToArray();
        int i;

        for (i = 0; i < collection.Length; i++)
        {
            if (i < childrenElement.Length)
            {
                VisualElement element = childrenElement[i];
                updater.Invoke(collection[i], element, true);
            }
            else
            {
                VisualElement element = itemAsset.Instantiate();
                container.Add(element);
                updater.Invoke(collection[i], element, false);
            }
        }

        for (; i < childrenElement.Length; i++)
        {
            container.Remove(childrenElement[i]);
        }
    }
}

public static class UI
{
    public static bool IsMouseHandled => IsUIFocused || IsPointerOverUI();

    public static bool IsUIFocused
    {
        get
        {
            if (GUIUtility.hotControl != 0) return true;

            foreach (UIDocument uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (uiDocument == null) continue;
                if (!uiDocument.gameObject.activeSelf) continue;
                if (!uiDocument.isActiveAndEnabled) continue;
                if (uiDocument.rootVisualElement == null) continue;
                if (uiDocument.rootVisualElement.focusController.focusedElement == null) continue;

                return true;
            }

            return false;
        }
    }

    public static bool IsPointerOverUI() => IsPointerOverUI(Input.mousePosition);
    public static bool IsPointerOverUI(Vector2 screenPos)
    {
        Vector2 pointerUiPos = new(screenPos.x, Screen.height - screenPos.y);
        List<VisualElement> picked = new();
        foreach (UIDocument uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (uiDocument == null) continue;
            if (!uiDocument.gameObject.activeSelf) continue;
            if (!uiDocument.isActiveAndEnabled) continue;
            if (uiDocument.rootVisualElement == null) continue;
            uiDocument.rootVisualElement.panel.PickAll(pointerUiPos, picked);
            foreach (VisualElement element in picked)
            {
                if (element == null) continue;
                if (element.resolvedStyle.backgroundColor.a == 0f) continue;
                if (!element.enabledInHierarchy) continue;
                return true;
            }
            picked.Clear();
        }
        return false;
    }
}
