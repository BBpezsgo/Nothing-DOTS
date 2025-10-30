using Unity.NetCode;

struct PingResponseRpc : IRpcCommand
{
    public required long Tick;
}
