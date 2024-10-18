using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedInstruction : IBufferElementData
{
    public readonly Instruction V;

    public BufferedInstruction(Instruction v)
    {
        V = v;
    }
}
