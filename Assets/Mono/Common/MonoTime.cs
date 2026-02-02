using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Burst;
using UnityEngine;

[SuppressMessage("Style", "IDE1006")]
public class MonoTime : MonoBehaviour
{
    class _nowKey { }
    class _ticksKey { }
    class _unixKey { }

    static readonly SharedStatic<float> _now = SharedStatic<float>.GetOrCreate<MonoTime, _nowKey>();
    static readonly SharedStatic<long> _ticks = SharedStatic<long>.GetOrCreate<MonoTime, _ticksKey>();
    static readonly SharedStatic<long> _unix = SharedStatic<long>.GetOrCreate<MonoTime, _unixKey>();

    public static float Now => _now.Data;
    public static long Ticks => _ticks.Data;
    public static long UnixSeconds => _unix.Data;

    void OnEnable() => Refresh();
    void Update() => Refresh();

    static void Refresh()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _now.Data = (float)now.TimeOfDay.TotalSeconds;
        _ticks.Data = now.Ticks;
        _unix.Data = now.ToUnixTimeSeconds();
    }
}
