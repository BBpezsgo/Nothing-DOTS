using Unity.NetCode;

struct PingResponseFinalRpc : IRpcCommand
{
    public required long Tick;
    public required byte Source;
}
