using System;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : UnityEngine.Object
{
    static T? _instance;

    public static T? InstanceOrNull => _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            return _instance == null ? throw new NullReferenceException($"Singleton {typeof(T).Name} is null") : _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null) throw new InvalidOperationException($"Singleton {typeof(T).Name} already exists");
        _instance = Instance;
    }
}
