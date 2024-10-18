using System;
using System.Linq;
using Unity.Entities;
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
