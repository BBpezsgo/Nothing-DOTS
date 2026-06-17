using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
struct WirelessTransmissionEventRpc : IRpcCommand
{
    public required float3 Origin;
    public required float3 Destination;
    public required FixedList32Bytes<byte> Data;
}
