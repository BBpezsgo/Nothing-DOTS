using UnityEngine;

interface IInspect<T>
{
    public T OnGUI(Rect rect, T value);
}
