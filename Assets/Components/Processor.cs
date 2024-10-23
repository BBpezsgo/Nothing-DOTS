using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public struct Processor : IComponentData
{
    public const int StackSize = 128;
    public const int HeapSize = 128;

    [GhostField] public FileId SourceFile;
    public Entity CompilerCache;
    public long SourceVersion;

    public Registers Registers;
    public FixedBytes510 Memory;

    [GhostField] public FixedString128Bytes StdOutBuffer;
}
