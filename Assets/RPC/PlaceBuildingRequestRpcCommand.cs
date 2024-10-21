using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct PlaceBuildingRequestRpc : IRpcCommand
{
    public float3 Position;
    public FixedString32Bytes BuildingName;
}
