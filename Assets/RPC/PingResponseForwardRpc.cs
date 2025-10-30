using Unity.NetCode;

struct PingResponseForwardRpc : IRpcCommand
{
    public required long Tick;
    public required byte Target;
}
