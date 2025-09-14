using UnityEngine;

interface IInspect<T>
{
    T OnGUI(Rect rect, T value);
}
