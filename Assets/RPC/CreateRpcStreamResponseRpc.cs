using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CreateRpcStreamResponseRpc : IRpcCommand
{
    public required FileResponseStatus Status;
    public required int TransactionId;
    public required FixedString128Bytes FileName;
}
