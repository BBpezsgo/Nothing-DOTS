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
    public MutableRange<int> AbsolutePosition;
    public DiagnosticsLevel Level;
    public FixedString512Bytes Message;
}
