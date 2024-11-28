
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class DebugEx
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Label(float3 position, string content)
    {
#if UNITY_EDITOR
        Handles.Label(position, content);
#endif
    }

    /// <summary>
    /// Square with edge of length 1
    /// </summary>
    static readonly ImmutableArray<float3> UnitSquare;
    /// <summary>
    /// Cube with edge of length 1
    /// </summary>
    static readonly ImmutableArray<float3> UnitCube;
    static readonly ImmutableArray<float3> UnitSphere;

    static DebugEx()
    {
        UnitSquare = ImmutableArray.Create<float3>(
            new(-0.5f, 0.5f, 0.0f),
            new(0.5f, 0.5f, 0.0f),
            new(0.5f, -0.5f, 0.0f),
            new(-0.5f, -0.5f, 0.0f)
        );

        System.ReadOnlySpan<float3> unitCube = stackalloc float3[]
        {
            new(-0.5f, 0.5f, -0.5f),
            new(0.5f, 0.5f, -0.5f),
            new(0.5f, -0.5f, -0.5f),
            new(-0.5f, -0.5f, -0.5f),

            new(-0.5f, 0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f),
            new(0.5f, -0.5f, 0.5f),
            new(-0.5f, -0.5f, 0.5f)
        };
        UnitCube = ImmutableArray.Create(unitCube);

        UnitSphere = ImmutableArray.Create(MakeUnitSphere(16));
    }

    static float3[] MakeUnitSphere(int len)
    {
        Debug.Assert(len > 2f);
        float3[] v = new float3[len * 3];
        for (int i = 0; i < len; i++)
        {
            float f = i / (float)len;
            float c = math.cos(f * math.PI * 2f);
            float s = math.sin(f * math.PI * 2f);
            v[(0 * len) + i] = new float3(c, s, 0f);
            v[(1 * len) + i] = new float3(0f, c, s);
            v[(2 * len) + i] = new float3(s, 0f, c);
        }
        return v;
    }

    public static void DrawSphere(float3 pos, float radius, Color color, float duration = 0f, bool depthTest = true)
    {
        int len = UnitSphere.Length / 3;
        for (int i = 0; i < len; i++)
        {
            float3 sX = pos + (radius * UnitSphere[(0 * len) + i]);
            float3 eX = pos + (radius * UnitSphere[(0 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sX, eX, color, duration);

            float3 sY = pos + (radius * UnitSphere[(1 * len) + i]);
            float3 eY = pos + (radius * UnitSphere[(1 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sY, eY, color, duration);

            float3 sZ = pos + (radius * UnitSphere[(2 * len) + i]);
            float3 eZ = pos + (radius * UnitSphere[(2 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sZ, eZ, color, duration);
        }
    }

    public static void DrawBox(AABB aabb, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(aabb.Center, aabb.Size, color, duration, depthTest);

    public static void DrawBox(AABB aabb, float3 offset, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(aabb.Center + offset, aabb.Size, color, duration, depthTest);

    public static void DrawBox(Bounds bounds, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(bounds.center, bounds.size, color, duration, depthTest);

    public static void DrawBox(float3 pos, float3 size, Color color, float duration = 0f, bool depthTest = true)
    {
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (UnitCube[i] * size);
            float3 e = pos + (UnitCube[(i + 1) % 4] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (UnitCube[4 + i] * size);
            float3 e = pos + (UnitCube[4 + ((i + 1) % 4)] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            float3 s = pos + (UnitCube[i] * size);
            float3 e = pos + (UnitCube[i + 4] * size);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
    }

    public static void DrawAxes(float3 pos, float scale = 1f, float duration = 0f, bool depthTest = true)
    {
        Debug.DrawLine(pos, pos + new float3(scale, 0f, 0f), Color.red, duration, depthTest);
        Debug.DrawLine(pos, pos + new float3(0f, scale, 0f), Color.green, duration, depthTest);
        Debug.DrawLine(pos, pos + new float3(0f, 0f, scale), Color.blue, duration, depthTest);
    }

    public static void DrawPoint(float3 position, float scale, Color color, float duration = 0f, bool depthTest = true)
    {
        float3 right = new(scale, 0f, 0f);
        float3 up = new(0f, scale, 0f);
        float3 forward = new(0f, 0f, scale);

        Debug.DrawLine(position - up, position + up, color, duration, depthTest);
        Debug.DrawLine(position - right, position + right, color, duration, depthTest);
        Debug.DrawLine(position - forward, position + forward, color, duration, depthTest);
    }

    public static void DrawRectangle(float3 start, float3 end, Color color, float duration = 0f, bool depthTest = true)
    {
        Debug.DrawLine(start, new float3(start.x, 0f, end.z), color, duration, depthTest);
        Debug.DrawLine(start, new float3(end.x, 0f, start.z), color, duration, depthTest);
        Debug.DrawLine(new float3(start.x, 0f, end.z), end, color, duration, depthTest);
        Debug.DrawLine(new float3(end.x, 0f, start.z), end, color, duration, depthTest);
    }

    public static void DrawFOV(float3 origin, float3 direction, float angle, float distance, Color color, float duration = 0f, bool depthTest = true)
    {
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
    }
}
