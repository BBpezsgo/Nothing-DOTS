using LanguageCore;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CompilationAnalysticsRpc : IRpcCommand
{
    public FileId Source;
    public FileId FileName;
    public LanguageCore.MutableRange<SinglePosition> Position;
    public LanguageCore.MutableRange<int> AbsolutePosition;
    public DiagnosticsLevel Level;
    public FixedString512Bytes Message;
}
