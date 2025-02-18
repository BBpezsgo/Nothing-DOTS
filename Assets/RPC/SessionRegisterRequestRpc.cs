using Unity.Collections;
using Unity.NetCode;

public struct SessionRegisterRequestRpc : IRpcCommand
{
    public required FixedString32Bytes Nickname;
}
