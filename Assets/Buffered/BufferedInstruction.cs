using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct BufferedInstruction : IBufferElementData
{
    public Instruction V;
}
