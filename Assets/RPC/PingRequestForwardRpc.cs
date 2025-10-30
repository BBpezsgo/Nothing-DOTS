using Unity.NetCode;

struct PingRequestForwardRpc : IRpcCommand
{
    public required long Tick;
    public required byte Source;
}
