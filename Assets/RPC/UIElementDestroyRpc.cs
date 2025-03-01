using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct UIElementDestroyRpc : IRpcCommand
{
    public required int Id;
}
