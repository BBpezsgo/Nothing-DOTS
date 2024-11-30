using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Serialization;
using UnityEngine;

public static partial class DebugEx
{
    public static void Label(float3 position, string text)
    {
        DebugDrawer? drawer = DebugDrawer.InstanceOrNull;
        if (drawer == null) return;
        drawer._labels.Add(new DebugDrawer.DebugLabel(
            text,
            11,
            Color.white,
            position,
            MonoTime.Now,
            MonoTime.Now + 1f
        ));
    }
}

public class DebugDrawer : Singleton<DebugDrawer>
{
    public readonly struct DebugLabel
    {
        public readonly string Text;
        public readonly int TextSize;
        public readonly Color Color;

        public readonly Vector3 Position;

        public readonly float BornTime;
        public readonly float DestroyTime;

        public DebugLabel(string text, int textSize, Color color, Vector3 position, float bornTime, float destroyTime)
        {
            Text = text;
            TextSize = textSize;
            Color = color;
            Position = position;
            BornTime = bornTime;
            DestroyTime = destroyTime;
        }
    }

    [DontSerialize] public List<DebugLabel> _labels = new();

    void OnDrawGizmos()
    {
        float now = MonoTime.Now;
        for (int i = _labels.Count - 1; i >= 0; i--)
        {
            DebugLabel label = _labels[i];
            if (now >= label.DestroyTime)
            {
                _labels.RemoveAt(i);
                continue;
            }

            Vector3 screenPosition = MainCamera.Camera.WorldToScreenPoint(label.Position);
            if (screenPosition.y < 0 || screenPosition.y > MainCamera.Camera.pixelHeight || screenPosition.x < 0 || screenPosition.x > MainCamera.Camera.pixelWidth || screenPosition.z < 0)
            { continue; }
            float pixelRatio = UnityEditor.HandleUtility.GUIPointToScreenPixelCoordinate(Vector2.right).x - UnityEditor.HandleUtility.GUIPointToScreenPixelCoordinate(Vector2.zero).x;
            UnityEditor.Handles.BeginGUI();
            GUIStyle style = new(GUI.skin.label)
            {
                fontSize = label.TextSize,
                normal = new GUIStyleState() { textColor = label.Color }
            };
            Vector2 size = style.CalcSize(new GUIContent(label.Text)) * pixelRatio;
            Vector2 alignedPosition =
                ((Vector2)screenPosition +
                size * ((Vector2.left + Vector2.up) / 2f)) * (Vector2.right + Vector2.down) +
                Vector2.up * MainCamera.Camera.pixelHeight;
            GUI.Label(new Rect(alignedPosition / pixelRatio, size / pixelRatio), label.Text, style);
            UnityEditor.Handles.EndGUI();
        }
    }
}
