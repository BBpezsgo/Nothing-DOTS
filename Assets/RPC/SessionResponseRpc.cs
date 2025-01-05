using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum SessionStatusCode : byte
{
    OK,
    AlreadyLoggedIn,
    InvalidGuid,
}

[BurstCompile]
public struct SessionResponseRpc : IRpcCommand
{
    public required SessionStatusCode StatusCode;
    public required FixedBytes16 Guid;
}
