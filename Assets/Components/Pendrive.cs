using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.NetCode;

[GhostComponent]
public struct Pendrive : IComponentData
{
    [GhostField] public int Id;
    [GhostField] public FixedBytes1024 Data;

    public unsafe Span<byte> Span => new(Unsafe.AsPointer(ref Data), 1024);
}
