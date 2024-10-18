using System;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;

#nullable enable

public struct Processor : IComponentData
{
    public const int StackSize = 128;
    public const int HeapSize = 128;

    public FixedString64Bytes SourceFile;
    public Entity CompilerCache;
    public DateTime SourceVersion;
    public Registers Registers;
    public float SleepUntil;
    public FixedString64Bytes StdOutBuffer;
    public FixedBytes510 Memory;
}
