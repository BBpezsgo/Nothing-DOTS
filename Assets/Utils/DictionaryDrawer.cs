#if UNITY_EDITOR

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public abstract class DictionaryDrawer<TK, TV> : PropertyDrawer
    where TK : notnull
{
    [NotNull] SerializableDictionary<TK, TV>? Dictionary = default;
    bool Foldout = default;
    const float ButtonWidth = 18f;
    static readonly FrozenDictionary<Type, Func<Rect, object, object>> FieldDrawers = new Dictionary<Type, Func<Rect, object, object>>()
    {
        { typeof(long), (rect, value) => EditorGUI.LongField(rect, (long)value) },
        { typeof(int), (rect, value) => EditorGUI.IntField(rect, (int)value) },
        { typeof(float), (rect, value) => EditorGUI.FloatField(rect, (float)value) },
        { typeof(double), (rect, value) => EditorGUI.DoubleField(rect, (double)value) },
        { typeof(string), (rect, value) => EditorGUI.TextField(rect, (string)value) },
        { typeof(bool), (rect, value) => EditorGUI.Toggle(rect, (bool)value) },
        { typeof(Vector2), (rect, value) => EditorGUI.Vector2Field(rect, GUIContent.none, (Vector2)value) },
        { typeof(Vector3), (rect, value) => EditorGUI.Vector3Field(rect, GUIContent.none, (Vector3)value) },
        { typeof(Vector4), (rect, value) => EditorGUI.Vector4Field(rect, GUIContent.none, (Vector4)value) },
        { typeof(Bounds), (rect, value) => EditorGUI.BoundsField(rect, (Bounds)value) },
        { typeof(Rect), (rect, value) => EditorGUI.RectField(rect, (Rect)value) },
    }.ToFrozenDictionary();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        CheckInitialize(property, label);
        if (Foldout)
        {
            return (Dictionary.Count + 1) * 17f;
        }
        return 17f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CheckInitialize(property, label);

        position.height = 17f;

        if (Dictionary is null)
        {
            position.y += 17f;
            GUI.Label(position, "null");
            return;
        }

        Rect foldoutRect = position;
        foldoutRect.width -= 2 * ButtonWidth;
        EditorGUI.BeginChangeCheck();
        Foldout = EditorGUI.Foldout(foldoutRect, Foldout, label, true);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(label.text, Foldout);
        }

        Rect buttonRect = position;
        buttonRect.x = position.width - ButtonWidth + position.x;
        buttonRect.width = ButtonWidth + 2;

        if (GUI.Button(buttonRect, new GUIContent("+", "Add item"), EditorStyles.miniButton))
        {
            AddNewItem();
        }

        buttonRect.x -= ButtonWidth;

        if (GUI.Button(buttonRect, new GUIContent(EditorGUIUtility.IconContent("d_Grid.EraserTool").image, "Clear dictionary"), EditorStyles.miniButtonRight))
        {
            ClearDictionary();
        }

        if (!Foldout) return;

        foreach (KeyValuePair<TK, TV> item in Dictionary)
        {
            TK key = item.Key;
            TV value = item.Value;

            position.y += 17f;

            Rect keyRect = position;
            keyRect.width /= 2;
            keyRect.width -= 4;
            EditorGUI.BeginChangeCheck();
            bool t = GUI.enabled;
            GUI.enabled = false;
            TK? newKey = DoField(keyRect, typeof(TK), key);
            GUI.enabled = t;
            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    Dictionary.Remove(key);
                    Dictionary.Add(newKey, value);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message);
                }
                break;
            }

            Rect valueRect = position;
            valueRect.x = position.width / 2 + 15;
            valueRect.width = keyRect.width - ButtonWidth;
            EditorGUI.BeginChangeCheck();
            value = DoField(valueRect, typeof(TV), value);
            if (EditorGUI.EndChangeCheck())
            {
                Dictionary[key] = value;
                break;
            }

            Rect removeRect = valueRect;
            removeRect.x = valueRect.xMax + 2;
            removeRect.width = ButtonWidth;
            if (GUI.Button(removeRect, new GUIContent("x", "Remove item"), EditorStyles.miniButtonRight))
            {
                RemoveItem(key);
                break;
            }
        }
    }

    void RemoveItem(TK key)
    {
        Dictionary.Remove(key);
    }

    void CheckInitialize(SerializedProperty property, GUIContent label)
    {
        if (Dictionary != null) return;

        UnityObject target = property.serializedObject.targetObject;
        Dictionary = fieldInfo.GetValue(target) as SerializableDictionary<TK, TV>;
        if (Dictionary == null)
        {
            Dictionary = new SerializableDictionary<TK, TV>();
            fieldInfo.SetValue(target, Dictionary);
        }

        Foldout = EditorPrefs.GetBool(label.text);
    }

    static T DoField<T>(Rect rect, Type type, T value)
    {
        if (FieldDrawers.TryGetValue(type, out Func<Rect, object, object>? field))
        { return (T)field(rect, value!); }

        if (type.IsEnum)
        { return (T)(object)EditorGUI.EnumPopup(rect, (Enum)(object)value!); }

        if (typeof(UnityObject).IsAssignableFrom(type))
        { return (T)(object)EditorGUI.ObjectField(rect, (UnityObject)(object)value!, type, true); }

        if (typeof(IInspect<T>).IsAssignableFrom(type))
        { return (T)(object)((IInspect<T>)(object)value!).OnGUI(rect, value)!; }

        Debug.Log("Type is not supported: " + type);
        return value;
    }

    void ClearDictionary()
    {
        Dictionary.Clear();
    }

    void AddNewItem()
    {
        TK key;
        if (typeof(TK) == typeof(string))
        { key = (TK)(object)string.Empty; }
        else
        { key = default!; }

        try
        {
            Dictionary.Add(key, default!);
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }
}

#endif
