using System;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static partial class DebugEx
{
#if UNITY_EDITOR && EDITOR_DEBUG
    readonly struct UnitSquare
    {
        readonly float3 _0;
        readonly float3 _1;
        readonly float3 _2;
        readonly float3 _3;

        public float3 this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => i switch
            {
                0 => _0,
                1 => _1,
                2 => _2,
                3 => _3,
                _ => throw new IndexOutOfRangeException(),
            };
        }

        public UnitSquare(float3 _0, float3 _1, float3 _2, float3 _3)
        {
            this._0 = _0;
            this._1 = _1;
            this._2 = _2;
            this._3 = _3;
        }
    }

    readonly struct UnitCube
    {
        readonly float3 _0;
        readonly float3 _1;
        readonly float3 _2;
        readonly float3 _3;
        readonly float3 _4;
        readonly float3 _5;
        readonly float3 _6;
        readonly float3 _7;

        public float3 this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => i switch
            {
                0 => _0,
                1 => _1,
                2 => _2,
                3 => _3,
                4 => _4,
                5 => _5,
                6 => _6,
                7 => _7,
                _ => throw new IndexOutOfRangeException(),
            };
        }

        public UnitCube(float3 _0, float3 _1, float3 _2, float3 _3, float3 _4, float3 _5, float3 _6, float3 _7)
        {
            this._0 = _0;
            this._1 = _1;
            this._2 = _2;
            this._3 = _3;
            this._4 = _4;
            this._5 = _5;
            this._6 = _6;
            this._7 = _7;
        }
    }

    static readonly UnitSquare _unitSquare;
    static readonly UnitCube _unitCube;
    static readonly FixedList512Bytes<float3> _unitSphere;

    static DebugEx()
    {
        _unitSquare = new(
            new(-0.5f, 0.5f, 0.0f),
            new(0.5f, 0.5f, 0.0f),
            new(0.5f, -0.5f, 0.0f),
            new(-0.5f, -0.5f, 0.0f)
        );

        _unitCube = new(
            new(-0.5f, 0.5f, -0.5f),
            new(0.5f, 0.5f, -0.5f),
            new(0.5f, -0.5f, -0.5f),
            new(-0.5f, -0.5f, -0.5f),

            new(-0.5f, 0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f),
            new(0.5f, -0.5f, 0.5f),
            new(-0.5f, -0.5f, 0.5f)
        );

        // 3*4*4*3 = 504
        MakeUnitSphere(14, ref _unitSphere);
    }

    /// <param name="len">
    /// <b>Must be in range 3..14 inclusive!!!</b>
    /// </param>
    static void MakeUnitSphere([AssumeRange(3, 14)] int len, ref FixedList512Bytes<float3> result)
    {
        result.Length = len * 3;
        for (int i = 0; i < len; i++)
        {
            float f = i / (float)len;
            float c = math.cos(f * math.PI * 2f);
            float s = math.sin(f * math.PI * 2f);
            result[(0 * len) + i] = new float3(c, s, 0f);
            result[(1 * len) + i] = new float3(0f, c, s);
            result[(2 * len) + i] = new float3(s, 0f, c);
        }
    }
#endif

    public static void DrawSphere(float3 pos, float radius, Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        int len = _unitSphere.Length / 3;
        for (int i = 0; i < len; i++)
        {
            float3 sX = pos + (radius * _unitSphere[(0 * len) + i]);
            float3 eX = pos + (radius * _unitSphere[(0 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sX, eX, color, duration);

            float3 sY = pos + (radius * _unitSphere[(1 * len) + i]);
            float3 eY = pos + (radius * _unitSphere[(1 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sY, eY, color, duration);

            float3 sZ = pos + (radius * _unitSphere[(2 * len) + i]);
            float3 eZ = pos + (radius * _unitSphere[(2 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sZ, eZ, color, duration);
        }
#endif
    }

    public static void DrawBox(AABB aabb, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(aabb.Center, aabb.Size, color, duration, depthTest);

    public static void DrawBox(AABB aabb, float3 offset, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(aabb.Center + offset, aabb.Size, color, duration, depthTest);

    public static void DrawBox(Bounds bounds, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(bounds.center, bounds.size, color, duration, depthTest);

    public static void DrawBox(float3 pos, float3 size, Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (_unitCube[i] * size);
            float3 e = pos + (_unitCube[(i + 1) % 4] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (_unitCube[4 + i] * size);
            float3 e = pos + (_unitCube[4 + ((i + 1) % 4)] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (_unitCube[i] * size);
            float3 e = pos + (_unitCube[i + 4] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
#endif
    }

    public static void DrawAxes(float3 pos, float scale = 1f, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        Debug.DrawLine(pos, pos + new float3(scale, 0f, 0f), Color.red, duration, depthTest);
        Debug.DrawLine(pos, pos + new float3(0f, scale, 0f), Color.green, duration, depthTest);
        Debug.DrawLine(pos, pos + new float3(0f, 0f, scale), Color.blue, duration, depthTest);
#endif
    }

    public static void DrawPoint(float3 position, float scale, Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        float3 right = new(scale, 0f, 0f);
        float3 up = new(0f, scale, 0f);
        float3 forward = new(0f, 0f, scale);

        Debug.DrawLine(position - up, position + up, color, duration, depthTest);
        Debug.DrawLine(position - right, position + right, color, duration, depthTest);
        Debug.DrawLine(position - forward, position + forward, color, duration, depthTest);
#endif
    }

    public static void DrawRectangle(float3 start, float3 end, Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        Debug.DrawLine(start, new float3(start.x, start.y, end.z), color, duration, depthTest);
        Debug.DrawLine(start, new float3(end.x, start.y, start.z), color, duration, depthTest);
        Debug.DrawLine(new float3(start.x, end.y, end.z), end, color, duration, depthTest);
        Debug.DrawLine(new float3(end.x, end.y, start.z), end, color, duration, depthTest);
#endif
    }

    public static void DrawFOV(float3 origin, float3 direction, float angle, float distance, Color color, float duration = 0f, bool depthTest = true)
    {
#if UNITY_EDITOR && EDITOR_DEBUG
        const float step = 0.01f;
        float directionAngle = math.atan2(direction.z, direction.x);
        float3 prevPoint = origin;
        if (angle < 0f) angle += math.TAU;
        for (float i = -angle; i <= angle; i += step)
        {
            float currentAngle = directionAngle + i;
            float3 point = new(math.cos(currentAngle), 0f, math.sin(currentAngle));
            point *= distance;
            Debug.DrawLine(prevPoint, point, color, duration, depthTest);
            prevPoint = point;
        }
        Debug.DrawLine(prevPoint, origin, color, duration, depthTest);
#endif
    }
}
