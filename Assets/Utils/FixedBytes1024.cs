using System;
using System.Runtime.InteropServices;
using Unity.Collections;

[Serializable]
[StructLayout(LayoutKind.Explicit, Size = 1024)]
public struct FixedBytes1024
{
    [FieldOffset(0)] public FixedBytes510 offset0000;
    [FieldOffset(510)] public FixedBytes510 offset0510;
    [FieldOffset(1020)] public byte offset1020;
    [FieldOffset(1021)] public byte offset1021;
    [FieldOffset(1022)] public byte offset1022;
    [FieldOffset(1023)] public byte offset1023;
}
