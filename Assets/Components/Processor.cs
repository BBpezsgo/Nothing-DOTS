using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public struct Processor : IComponentData
{
    public const int TotalMemorySize = 510;
    public const int StackSize = 256;
    public const int HeapSize = 128;

    public const int UserMemorySize = StackSize + HeapSize;
    public const int MappedMemoryStart = UserMemorySize;
    public const int MappedMemorySize = TotalMemorySize - UserMemorySize;

    [GhostField] public FileId SourceFile;
    public Entity CompilerCache;
    public long SourceVersion;

    public Registers Registers;
    public FixedBytes510 Memory;

    [GhostField] public FixedString128Bytes StdOutBuffer;
}
