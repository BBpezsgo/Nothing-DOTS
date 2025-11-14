using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Burst;
using UnityEngine;

[SuppressMessage("Style", "IDE1006")]
public class MonoTime : MonoBehaviour
{
    class _nowKey { }
    class _ticksKey { }
    static readonly SharedStatic<float> _now = SharedStatic<float>.GetOrCreate<MonoTime, _nowKey>();
    static readonly SharedStatic<long> _ticks = SharedStatic<long>.GetOrCreate<MonoTime, _ticksKey>();

    public static float Now => _now.Data;
    public static long Ticks => _ticks.Data;

    void OnEnable()
    {
        TimeSpan now = DateTime.UtcNow.TimeOfDay;
        _now.Data = (float)now.TotalSeconds;
        _ticks.Data = now.Ticks;
    }

    void Update()
    {
        TimeSpan now = DateTime.UtcNow.TimeOfDay;
        _now.Data = (float)now.TotalSeconds;
        _ticks.Data = now.Ticks;
    }
}
