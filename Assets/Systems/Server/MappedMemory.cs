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
/// Size: 24
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_GPS
{
    public float3 Position;
    public float3 Forward;
}

/// <summary>
/// Size: 1
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Pendrive
{
    public bool IsPlugged;
}

/// <summary>
/// Size: 1
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MappedMemory_Facility
{
    public const u8 SignalEnqueueHash = 1;
    public const u8 SignalDequeueHash = 2;
    public const u8 SignalDequeueSuccess = 3;
    public const u8 SignalDequeueFailure = 4;

    public u8 Signal;
    public i32 HashLocation;
}

[StructLayout(LayoutKind.Explicit)]
public struct MappedMemory
{
    [FieldOffset(0)] public MappedMemory_GPS GPS;
    [FieldOffset(24)] public MappedMemory_Pendrive Pendrive;
    [FieldOffset(25)] public MappedMemory_Radar Radar;
    [FieldOffset(33)] public MappedMemory_Vehicle Vehicle;

    const int GenericModulesSize = 35;

    [FieldOffset(GenericModulesSize)] public MappedMemory_CombatTurret CombatTurret;
    [FieldOffset(GenericModulesSize)] public MappedMemory_Extractor Extractor;
    [FieldOffset(GenericModulesSize)] public MappedMemory_Transporter Transporter;
    [FieldOffset(GenericModulesSize)] public MappedMemory_Facility Facility;
}
