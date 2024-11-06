using Unity.Burst;
using Unity.Collections;

[BurstCompile]
public readonly struct UnitCommandRequest
{
    public readonly int Id;
    public readonly ushort DataLength;
    public readonly FixedBytes30 Data;

    public UnitCommandRequest(int id, ushort dataLength, FixedBytes30 data)
    {
        Id = id;
        DataLength = dataLength;
        Data = data;
    }
}
