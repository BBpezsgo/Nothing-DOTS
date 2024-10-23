using LanguageCore;
using Maths;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

public enum CompilationAnalysticsItemType
{
    Error,
    Warning,
    Info,
    Hint,
}

[BurstCompile]
public struct BufferedCompilationAnalystics : IBufferElementData
{
    public FixedString64Bytes FileName;
    public MutableRange<SinglePosition> Position;

    public CompilationAnalysticsItemType Type;
    public FixedString128Bytes Message;
}
