using Unity.Collections;
using Unity.NetCode;

public struct ServerGuidResponseRpc : IRpcCommand
{
    public required FixedBytes16 Guid;
}
