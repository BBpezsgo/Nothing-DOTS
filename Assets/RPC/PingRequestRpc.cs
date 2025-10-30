using Unity.NetCode;

struct PingRequestRpc : IRpcCommand
{
    public required long Tick;
    public required byte Target;
}
