using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
struct WiredTransmissionEventRpc : IRpcCommand
{
    public required SpawnedGhost Origin;
    public required int OriginPort;
    public required SpawnedGhost Destination;
    public required int DestinationPort;
    public required FixedList64Bytes<byte> Data;
}
