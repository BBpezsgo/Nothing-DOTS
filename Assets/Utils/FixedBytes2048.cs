using System;
using System.Runtime.InteropServices;

[Serializable]
[StructLayout(LayoutKind.Explicit, Size = 2048)]
public struct FixedBytes2048
{
    [FieldOffset(0)] public FixedBytes1024 offset0000;
    [FieldOffset(1024)] public FixedBytes1024 offset01024;
}
