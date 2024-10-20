using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

#nullable enable

public struct PlaceBuildingRequestRpc : IRpcCommand
{
    public float3 Position;
    public FixedString32Bytes BuildingName;
}
