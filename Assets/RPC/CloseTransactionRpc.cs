using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CloseTransactionRpc : IRpcCommand
{
    public required int TransactionId;
}
