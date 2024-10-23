using LanguageCore;
using Maths;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CompilationAnalysticsRpc : IRpcCommand
{
    public FileId FileName;
    public MutableRange<SinglePosition> Position;

    public CompilationAnalysticsItemType Type;
    public FixedString128Bytes Message;
}
