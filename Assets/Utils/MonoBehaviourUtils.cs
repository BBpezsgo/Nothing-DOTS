using UnityEngine;
using UnityEngine.Assertions;

public static class MonoBehaviourUtils
{
    public static T GetRequiredComponent<T>(this MonoBehaviour o)
    {
        if (!o.TryGetComponent(out T component))
        {
            Debug.LogError($"No {nameof(T)} found on object");
            throw new AssertionException($"No {nameof(T)} found on object", null);
        }

        return component;
    }
}
