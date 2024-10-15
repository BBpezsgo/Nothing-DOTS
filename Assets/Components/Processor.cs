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
    public long CompiledAt;
    public double HotReloadAt;
    public BytecodeProcessor? BytecodeProcessor;
    public double SleepUntil;
    public FixedString64Bytes StdOutBuffer;

    public Span<byte> MappedMemory => BytecodeProcessor is null ? Span<byte>.Empty : ((Span<byte>)BytecodeProcessor.Memory)[(HeapSize + StackSize)..];
}
