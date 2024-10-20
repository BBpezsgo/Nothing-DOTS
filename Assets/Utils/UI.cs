using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

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
    public static bool IsMouseCaptured
    {
        get
        {
            if (GUIUtility.hotControl != 0) return true;

            UIDocument[] uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);

            foreach (UIDocument uiElement in uiDocuments)
            {
                if (uiElement == null) continue;
                if (!uiElement.gameObject.activeSelf) continue;
                if (!uiElement.isActiveAndEnabled) continue;
                if (uiElement.rootVisualElement == null) continue;
                if (uiElement.rootVisualElement.focusController.focusedElement == null) continue;

                return true;
            }

            return false;
        }
    }
}
