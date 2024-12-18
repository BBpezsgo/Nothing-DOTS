using LanguageCore;
using Maths;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedCompilationAnalystics : IBufferElementData
{
    public FixedString64Bytes FileName;
    public MutableRange<SinglePosition> Position;

    public DiagnosticsLevel Level;
    public FixedString128Bytes Message;
}
