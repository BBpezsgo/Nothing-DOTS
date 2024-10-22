using System;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;

#nullable enable

public struct Processor : IComponentData
{
    public const int StackSize = 128;
    public const int HeapSize = 128;

    public FileId SourceFile;
    public Entity CompilerCache;
    public long SourceVersion;

    public Registers Registers;
    public FixedBytes510 Memory;

    public FixedString128Bytes StdOutBuffer;
}
