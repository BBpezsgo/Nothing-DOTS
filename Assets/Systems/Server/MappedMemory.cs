using System.Runtime.InteropServices;
using Unity.Mathematics;

using u8 = System.Byte;
using i8 = System.SByte;
using u16 = System.UInt16;
using i16 = System.Int16;
using u32 = System.UInt32;
using i32 = System.Int32;
using f32 = System.Single;

/// <summary>
/// Size: 2
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Vehicle
{
    public i8 InputForward;
    public i8 InputSteer;
}

/// <summary>
/// Size: 17
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_CombatTurret
{
    public u8 InputShoot;
    public f32 TurretTargetRotation;
    public f32 TurretTargetAngle;
    public f32 TurretCurrentRotation;
    public f32 TurretCurrentAngle;
}

/// <summary>
/// Size: 1
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Extractor
{
    public u8 InputExtract;
}

/// <summary>
/// Size: 5
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Transporter
{
    public u8 LoadDirection;
    public i32 CurrentLoad;
}

/// <summary>
/// Size: 8
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Radar
{
    public f32 RadarDirection;
    public f32 RadarResponse;
}

/// <summary>
/// Size: 16
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_GPS
{
    public float2 Position;
    public float2 Forward;
}

[StructLayout(LayoutKind.Explicit)]
public struct MappedMemory
{
    [FieldOffset(0)] public MappedMemory_Vehicle Vehicle;
    [FieldOffset(2)] public MappedMemory_CombatTurret CombatTurret;
    [FieldOffset(2)] public MappedMemory_Extractor Extractor;
    [FieldOffset(2)] public MappedMemory_Transporter Transporter;
    [FieldOffset(19)] public MappedMemory_Radar Radar;
    [FieldOffset(27)] public MappedMemory_GPS GPS;
}
