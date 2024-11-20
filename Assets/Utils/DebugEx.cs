
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class DebugEx
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Label(float x, float y, float z, string content)
        => Label(new Vector3(x, y, z), content);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Label(Vector3 position, string content)
    {
#if UNITY_EDITOR
        Handles.Label(position, content);
#endif
    }

    /// <summary>
    /// Square with edge of length 1
    /// </summary>
    static readonly ImmutableArray<Vector3> UnitSquare = ImmutableArray.Create<Vector3>(
        new(-0.5f, 0.5f, 0.0f),
        new(0.5f, 0.5f, 0.0f),
        new(0.5f, -0.5f, 0.0f),
        new(-0.5f, -0.5f, 0.0f)
    );
    /// <summary>
    /// Cube with edge of length 1
    /// </summary>
    static readonly ImmutableArray<Vector3> UnitCube = ImmutableArray.Create<Vector3>(
        new(-0.5f, 0.5f, -0.5f),
        new(0.5f, 0.5f, -0.5f),
        new(0.5f, -0.5f, -0.5f),
        new(-0.5f, -0.5f, -0.5f),

        new(-0.5f, 0.5f, 0.5f),
        new(0.5f, 0.5f, 0.5f),
        new(0.5f, -0.5f, 0.5f),
        new(-0.5f, -0.5f, 0.5f)
    );
    static readonly ImmutableArray<Vector3> UnitSphere = ImmutableArray.Create(MakeUnitSphere(16));

    static Vector3[] MakeUnitSphere(int len)
    {
        Debug.Assert(len > 2);
        Vector3[] v = new Vector3[len * 3];
        for (int i = 0; i < len; i++)
        {
            float f = i / (float)len;
            float c = math.cos(f * math.PI * 2f);
            float s = math.sin(f * math.PI * 2f);
            v[(0 * len) + i] = new Vector3(c, s, 0);
            v[(1 * len) + i] = new Vector3(0, c, s);
            v[(2 * len) + i] = new Vector3(s, 0, c);
        }
        return v;
    }

    public static void DrawSphere(Vector3 pos, float radius, Color color, float duration = 0f, bool depthTest = true)
    {
        int len = UnitSphere.Length / 3;
        for (int i = 0; i < len; i++)
        {
            Vector3 sX = pos + (radius * UnitSphere[(0 * len) + i]);
            Vector3 eX = pos + (radius * UnitSphere[(0 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sX, eX, color, duration);

            Vector3 sY = pos + (radius * UnitSphere[(1 * len) + i]);
            Vector3 eY = pos + (radius * UnitSphere[(1 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sY, eY, color, duration);

            Vector3 sZ = pos + (radius * UnitSphere[(2 * len) + i]);
            Vector3 eZ = pos + (radius * UnitSphere[(2 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sZ, eZ, color, duration);
        }
    }

    public static void DrawBox(AABB aabb, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox((Vector3)aabb.Center, aabb.Size, color, duration, depthTest);

    public static void DrawBox(AABB aabb, float3 offset, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox((Vector3)(aabb.Center + offset), aabb.Size, color, duration, depthTest);

    public static void DrawBox(Bounds bounds, Color color, float duration = 0f, bool depthTest = true)
        => DrawBox(bounds.center, bounds.size, color, duration, depthTest);

    public static void DrawBox(Vector3 pos, Vector3 size, Color color, float duration = 0f, bool depthTest = true)
    {
        Vector3 sz = new(size.x, size.y, size.z);
        for (int i = 0; i < 4; i++)
        {
            Vector3 s = pos + Vector3.Scale(UnitCube[i], sz);
            Vector3 e = pos + Vector3.Scale(UnitCube[(i + 1) % 4], sz);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 s = pos + Vector3.Scale(UnitCube[4 + i], sz);
            Vector3 e = pos + Vector3.Scale(UnitCube[4 + ((i + 1) % 4)], sz);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 s = pos + Vector3.Scale(UnitCube[i], sz);
            Vector3 e = pos + Vector3.Scale(UnitCube[i + 4], sz);
            Debug.DrawLine(s, e, color, duration, depthTest);
        }
    }

    public static void DrawAxes(Vector3 pos, float scale = 1f, float duration = 0f, bool depthTest = true)
    {
        Debug.DrawLine(pos, pos + new Vector3(scale, 0, 0), Color.red, duration, depthTest);
        Debug.DrawLine(pos, pos + new Vector3(0, scale, 0), Color.green, duration, depthTest);
        Debug.DrawLine(pos, pos + new Vector3(0, 0, scale), Color.blue, duration, depthTest);
    }

    public static void DrawPoint(Vector3 position, float scale, Color color, float duration = 0f, bool depthTest = true)
    {
        Vector3 up = Vector3.up * scale;
        Vector3 right = Vector3.right * scale;
        Vector3 forward = Vector3.forward * scale;

        Debug.DrawLine(position - up, position + up, color, duration, depthTest);
        Debug.DrawLine(position - right, position + right, color, duration, depthTest);
        Debug.DrawLine(position - forward, position + forward, color, duration, depthTest);
    }

    public static void DrawRectangle(Vector3 start, Vector3 end, Color color, float duration = 0f, bool depthTest = true)
    {
        Debug.DrawLine(start, new Vector3(start.x, 0f, end.z), color, duration, depthTest);
        Debug.DrawLine(start, new Vector3(end.x, 0f, start.z), color, duration, depthTest);
        Debug.DrawLine(new Vector3(start.x, 0f, end.z), end, color, duration, depthTest);
        Debug.DrawLine(new Vector3(end.x, 0f, start.z), end, color, duration, depthTest);
    }
}
