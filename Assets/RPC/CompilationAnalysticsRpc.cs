using LanguageCore;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CompilationAnalysticsRpc : IRpcCommand
{
    public required FileId Source;
    public required FixedString512Bytes Message;
    public required DiagnosticsLevel Level;
    public required uint Id;
    public uint Parent;
    public FileId FileName;
    public MutableRange<SinglePosition> Position;
    public MutableRange<int> AbsolutePosition;
}
