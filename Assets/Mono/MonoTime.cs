using System;
using Unity.Burst;
using UnityEngine;

public class MonoTime : MonoBehaviour
{
    class _nowKey {}
    static readonly SharedStatic<float> _now = SharedStatic<float>.GetOrCreate<MonoTime, _nowKey>();

    public static float Now => _now.Data;

    void OnEnable()
    {
        _now.Data = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }

    void Update()
    {
        _now.Data = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }
}
