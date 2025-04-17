using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public static class GhostInstanceExtensions
{
    [BurstCompile]
    public static bool IsEquals(this in GhostInstance a, in GhostInstance b) => a.ghostId == b.ghostId && a.spawnTick == b.spawnTick;
}