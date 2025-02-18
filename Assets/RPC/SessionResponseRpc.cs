using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum SessionStatusCode : byte
{
    OK,
    AlreadyLoggedIn,
    InvalidGuid,
}

public static class SessionStatusCodeExtensions
{
    public static bool IsOk(this SessionStatusCode v) => v is SessionStatusCode.OK or SessionStatusCode.AlreadyLoggedIn;
}

[BurstCompile]
public struct SessionResponseRpc : IRpcCommand
{
    public required SessionStatusCode StatusCode;
    public required FixedBytes16 Guid;
    public required FixedString32Bytes Nickname;
}
