
using System;
using System.Runtime.CompilerServices;
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
#pragma warning disable IDE0052 // Remove unread private members
    static readonly Vector4[] s_UnitSquare =
#pragma warning restore IDE0052 // Remove unread private members
    {
            new(-0.5f, 0.5f, 0, 1),
            new(0.5f, 0.5f, 0, 1),
            new(0.5f, -0.5f, 0, 1),
            new(-0.5f, -0.5f, 0, 1),
        };
    /// <summary>
    /// Cube with edge of length 1
    /// </summary>
    static readonly Vector4[] s_UnitCube =
    {
            new(-0.5f,  0.5f, -0.5f, 1),
            new(0.5f,  0.5f, -0.5f, 1),
            new(0.5f, -0.5f, -0.5f, 1),
            new(-0.5f, -0.5f, -0.5f, 1),

            new(-0.5f,  0.5f,  0.5f, 1),
            new(0.5f,  0.5f,  0.5f, 1),
            new(0.5f, -0.5f,  0.5f, 1),
            new(-0.5f, -0.5f,  0.5f, 1)
        };
    static readonly Vector4[] s_UnitSphere = MakeUnitSphere(16);

    static Vector4[] MakeUnitSphere(int len)
    {
        Debug.Assert(len > 2);
        Vector4[] v = new Vector4[len * 3];
        for (int i = 0; i < len; i++)
        {
            float f = i / (float)len;
            float c = MathF.Cos(f * MathF.PI * 2f);
            float s = MathF.Sin(f * MathF.PI * 2f);
            v[(0 * len) + i] = new Vector4(c, s, 0, 1);
            v[(1 * len) + i] = new Vector4(0, c, s, 1);
            v[(2 * len) + i] = new Vector4(s, 0, c, 1);
        }
        return v;
    }

    public static void DrawSphere(Vector4 pos, float radius, Color color)
    {
        Vector4[] v = s_UnitSphere;
        int len = v.Length / 3;
        for (int i = 0; i < len; i++)
        {
            Vector4 sX = pos + (radius * v[(0 * len) + i]);
            Vector4 eX = pos + (radius * v[(0 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sX, eX, color);

            Vector4 sY = pos + (radius * v[(1 * len) + i]);
            Vector4 eY = pos + (radius * v[(1 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sY, eY, color);

            Vector4 sZ = pos + (radius * v[(2 * len) + i]);
            Vector4 eZ = pos + (radius * v[(2 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sZ, eZ, color);
        }
    }

    public static void DrawSphere(Vector4 pos, float radius, Color color, float duration)
    {
        Vector4[] v = s_UnitSphere;
        int len = v.Length / 3;
        for (int i = 0; i < len; i++)
        {
            Vector4 sX = pos + (radius * v[(0 * len) + i]);
            Vector4 eX = pos + (radius * v[(0 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sX, eX, color, duration);

            Vector4 sY = pos + (radius * v[(1 * len) + i]);
            Vector4 eY = pos + (radius * v[(1 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sY, eY, color, duration);

            Vector4 sZ = pos + (radius * v[(2 * len) + i]);
            Vector4 eZ = pos + (radius * v[(2 * len) + ((i + 1) % len)]);
            Debug.DrawLine(sZ, eZ, color, duration);
        }
    }

    public static void DrawBox(Unity.Mathematics.AABB aabb, Color color) => DrawBox((Vector3)aabb.Center, aabb.Size, color);

    public static void DrawBox(Vector4 pos, Vector3 size, Color color)
    {
        Vector4[] v = s_UnitCube;
        Vector4 sz = new(size.x, size.y, size.z, 1);
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[i], sz);
            Vector4 e = pos + Vector4.Scale(v[(i + 1) % 4], sz);
            Debug.DrawLine(s, e, color);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[4 + i], sz);
            Vector4 e = pos + Vector4.Scale(v[4 + ((i + 1) % 4)], sz);
            Debug.DrawLine(s, e, color);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[i], sz);
            Vector4 e = pos + Vector4.Scale(v[i + 4], sz);
            Debug.DrawLine(s, e, color);
        }
    }

    public static void DrawBox(Vector4 pos, Vector3 size, Color color, float duration)
    {
        Vector4[] v = s_UnitCube;
        Vector4 sz = new(size.x, size.y, size.z, 1);
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[i], sz);
            Vector4 e = pos + Vector4.Scale(v[(i + 1) % 4], sz);
            Debug.DrawLine(s, e, color, duration);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[4 + i], sz);
            Vector4 e = pos + Vector4.Scale(v[4 + ((i + 1) % 4)], sz);
            Debug.DrawLine(s, e, color, duration);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector4 s = pos + Vector4.Scale(v[i], sz);
            Vector4 e = pos + Vector4.Scale(v[i + 4], sz);
            Debug.DrawLine(s, e, color, duration);
        }
    }

    public static void DrawBox(Bounds bounds, Color color)
        => DrawBox(bounds.center, bounds.size, color);

    public static void DrawBox(Bounds bounds, Color color, float duration)
        => DrawBox(bounds.center, bounds.size, color, duration);

    public static void DrawAxes(Vector4 pos)
        => DrawAxes(pos, 1f);

    public static void DrawAxes(Vector4 pos, float scale)
    {
        Debug.DrawLine(pos, pos + new Vector4(scale, 0, 0), Color.red);
        Debug.DrawLine(pos, pos + new Vector4(0, scale, 0), Color.green);
        Debug.DrawLine(pos, pos + new Vector4(0, 0, scale), Color.blue);
    }

    public static void DrawAxes(Vector4 pos, float scale, float duration)
    {
        Debug.DrawLine(pos, pos + new Vector4(scale, 0, 0), Color.red, duration);
        Debug.DrawLine(pos, pos + new Vector4(0, scale, 0), Color.green, duration);
        Debug.DrawLine(pos, pos + new Vector4(0, 0, scale), Color.blue, duration);
    }

    public static void DrawPoint(Vector3 position, float scale, Color color)
    {
        Vector3 up = Vector3.up * scale;
        Vector3 right = Vector3.right * scale;
        Vector3 forward = Vector3.forward * scale;

        Debug.DrawLine(position - up, position + up, color);
        Debug.DrawLine(position - right, position + right, color);
        Debug.DrawLine(position - forward, position + forward, color);
    }

    public static void DrawPoint(Vector3 position, float scale, Color color, float duration)
    {
        Vector3 up = Vector3.up * scale;
        Vector3 right = Vector3.right * scale;
        Vector3 forward = Vector3.forward * scale;

        Debug.DrawLine(position - up, position + up, color, duration);
        Debug.DrawLine(position - right, position + right, color, duration);
        Debug.DrawLine(position - forward, position + forward, color, duration);
    }
}
