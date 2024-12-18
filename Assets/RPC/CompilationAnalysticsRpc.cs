using LanguageCore;
using Maths;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CompilationAnalysticsRpc : IRpcCommand
{
    public required DiagnosticsLevel Level;
    public required FixedString512Bytes Message;
    public FileId FileName;
    public MutableRange<SinglePosition> Position;
    public MutableRange<int> AbsolutePosition;
}
