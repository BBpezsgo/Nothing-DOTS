using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Unity.Collections;
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
        => SyncList(
            container,
            collection.AsNativeArray(),
            itemAsset,
            updater
        );

    public static void SyncList<T>(
        this VisualElement container,
        NativeList<T> collection,
        VisualTreeAsset itemAsset,
        Action<T, VisualElement, bool> updater)
        where T : unmanaged
        => SyncList(
            container,
            collection.AsArray(),
            itemAsset,
            updater
        );

    public static void SyncList<T>(
        this VisualElement container,
        NativeArray<T> collection,
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
    static ImmutableArray<UIDocument> _uiDocuments;
    static ImmutableArray<UIDocument> UIDocuments => _uiDocuments.IsDefault ? (_uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToImmutableArray()) : _uiDocuments;

    public static bool IsMouseHandled => IsUIFocused || IsPointerOverUI();

    public static bool IsUIFocused
    {
        get
        {
            if (GUIUtility.hotControl != 0) return true;

            foreach (UIDocument uiDocument in UIDocuments)
            {
                if (uiDocument.rootVisualElement == null) continue;
                if (uiDocument.rootVisualElement.focusController.focusedElement == null) continue;

                return true;
            }

            return false;
        }
    }

    static List<VisualElement> _picked = new();

    public static bool IsPointerOverUI() => IsPointerOverUI(Input.mousePosition);
    public static bool IsPointerOverUI(Vector2 screenPos)
    {
        Vector2 pointerUiPos = new(screenPos.x, Screen.height - screenPos.y);
        foreach (UIDocument uiDocument in UIDocuments)
        {
            if (uiDocument.rootVisualElement == null) continue;
            _picked.Clear();
            uiDocument.rootVisualElement.panel.PickAll(pointerUiPos, _picked);
            foreach (VisualElement element in _picked)
            {
                if (element == null) continue;
                if (element.resolvedStyle.backgroundColor.a == 0f) continue;
                if (!element.enabledInHierarchy) continue;
                return true;
            }
            _picked.Clear();
        }
        return false;
    }
}
