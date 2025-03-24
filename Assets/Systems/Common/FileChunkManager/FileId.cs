using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

[BurstCompile]
public struct FileId : IEquatable<FileId>, IInspect<FileId>
{
    public FixedString128Bytes Name;
    public NetcodeEndPoint Source;

    public FileId(FixedString128Bytes name, NetcodeEndPoint source)
    {
        Name = name;
        Source = source;
    }

    public override readonly int GetHashCode() => HashCode.Combine(Name, Source);
    public override readonly string ToString() => $"{Source} {Name}";
    public override readonly bool Equals(object obj) => obj is FileId other && Equals(other);
    public readonly bool Equals(FileId other) => Name.Equals(other.Name) && Source.Equals(other.Source);

    public readonly FileId OnGUI(Rect rect, FileId value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.TextField(rect, value.Name.ToString());
        GUI.enabled = t;
#endif
        return value;
    }

    public static bool operator ==(FileId a, FileId b) => a.Equals(b);
    public static bool operator !=(FileId a, FileId b) => !a.Equals(b);
}
