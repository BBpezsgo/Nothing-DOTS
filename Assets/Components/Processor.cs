using System;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;

#nullable enable

public class Processor : IComponentData
{
    public const int StackSize = 128;
    public const int HeapSize = 128;

    public FixedString64Bytes SourceFile;
    public bool CompileSecuedued;
    public DateTime SourceVersion;
    public float HotReloadAt;
    public BytecodeProcessor? BytecodeProcessor;
    public float SleepUntil;
    public FixedString64Bytes StdOutBuffer;

    public Span<byte> MappedMemory => BytecodeProcessor is null ? Span<byte>.Empty : ((Span<byte>)BytecodeProcessor.Memory)[(HeapSize + StackSize)..];
}
