using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

struct StatusLED
{
    [GhostField(SendData = false)] public Entity LED;
    [GhostField] public int Status;

    public StatusLED(Entity led)
    {
        LED = led;
        Status = default;
    }
}

struct BlinkingLED
{
    [GhostField(SendData = false)] public Entity LED;
    [GhostField(SendData = false)] public float LastBlinked;
    [GhostField(SendData = false)] public uint LastBlinkedLocal;
    [GhostField] public uint LastBlinkedNet;

    public BlinkingLED(Entity led)
    {
        LED = led;
    }

    public void Blink() => LastBlinkedNet = unchecked(LastBlinkedNet + 1);
    public bool ReceiveBlink()
    {
        if (LastBlinkedLocal != LastBlinkedNet)
        {
            LastBlinkedLocal = LastBlinkedNet;
            LastBlinked = Math.Max(LastBlinkedNet, MonoTime.Now);
            return true;
        }
        return false;
    }
}

[StructLayout(LayoutKind.Explicit)]
struct ProcessorMemory
{
    [FieldOffset(0)] public FixedBytes2048 Memory;
    [FieldOffset(Processor.MappedMemoryStart)] public MappedMemory MappedMemory;
}

struct Processor : IComponentData
{
    public const int TotalMemorySize = 2048;
    public const int HeapSize = 512;
    public const int StackSize = 1024;

    public const int UserMemorySize = StackSize + HeapSize;
    public const int MappedMemoryStart = UserMemorySize;
    public const int MappedMemorySize = TotalMemorySize - UserMemorySize;

    [GhostField] public FileId SourceFile;
    public long CompiledSourceVersion;

    public Registers Registers;
    public ProcessorMemory Memory;
    public FixedList128Bytes<BufferedUnitTransmission> IncomingTransmissions;
    public FixedList128Bytes<BufferedUnitTransmissionOutgoing> OutgoingTransmissions;
    public FixedList128Bytes<UnitCommandRequest> CommandQueue;
    [GhostField] public int Crash;
    [GhostField] public Signal Signal;
    public bool SignalNotified;

    public bool PendrivePlugRequested;
    public bool PendriveUnplugRequested;
    public (bool Write, Pendrive Pendrive, Entity Entity) PluggedPendrive;

    public bool IsKeyRequested;
    public FixedList128Bytes<char> InputKey;

    /// <summary>
    /// XZ direction in local space
    /// </summary>
    public float2 RadarRequest;
    public float3 RadarResponse;

    [GhostField] public FixedString128Bytes StdOutBuffer;

    [GhostField] public StatusLED StatusLED;
    [GhostField] public BlinkingLED NetworkReceiveLED;
    [GhostField] public BlinkingLED NetworkSendLED;
    [GhostField] public BlinkingLED RadarLED;
    [GhostField] public BlinkingLED USBLED;
    public float3 USBPosition;
    public quaternion USBRotation;

    public static unsafe nint GetMemoryPtr(ref Processor processor) => (nint)Unsafe.AsPointer(ref processor.Memory);
}
