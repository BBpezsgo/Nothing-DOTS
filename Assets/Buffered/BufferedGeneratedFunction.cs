using LanguageCore.Runtime;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[ChunkSerializable]
public struct BufferedGeneratedFunction : IBufferElementData
{
    public ExternalFunctionScopedSync V;
}
