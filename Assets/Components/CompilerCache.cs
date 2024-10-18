using System;
using Unity.Collections;
using Unity.Entities;

public struct CompilerCache : IComponentData
{
    public FixedString64Bytes SourceFile;
    public DateTime Version;
    public bool CompileSecuedued;
    public float HotReloadAt;
}
