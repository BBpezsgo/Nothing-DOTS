using LanguageCore.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct StatusLED
{
    [GhostField(SendData = false)] public Entity LED;
    [GhostField] public int Status;

    public StatusLED(Entity led)
    {
        LED = led;
        Status = default;
    }
}

public struct BlinkingLED
{
    [GhostField(SendData = false)] public Entity LED;
    [GhostField] public float LastBlinked;

    public BlinkingLED(Entity led)
    {
        LED = led;
        LastBlinked = default;
    }

    public void Blink() => LastBlinked = MonoTime.Now;
    public readonly bool IsOn(float now) => now - LastBlinked < 0.05f;
}

public struct Processor : IComponentData
{
    public const int TotalMemorySize = 1024;
    public const int StackSize = 512;
    public const int HeapSize = 128;

    public const int UserMemorySize = StackSize + HeapSize;
    public const int MappedMemoryStart = UserMemorySize;
    public const int MappedMemorySize = TotalMemorySize - UserMemorySize;

    [GhostField] public FileId SourceFile;
    public long CompiledSourceVersion;

    public Registers Registers;
    public FixedBytes1024 Memory;
    public FixedList128Bytes<BufferedUnitTransmission> IncomingTransmissions;
    public FixedList128Bytes<BufferedUnitTransmissionOutgoing> OutgoingTransmissions;
    public FixedList128Bytes<UnitCommandRequest> CommandQueue;
    [GhostField] public int Crash;
    [GhostField] public Signal Signal;
    public bool SignalNotified;

    [GhostField] public bool IsKeyRequested;
    public char InputKey;

    /// <summary>
    /// Direction in local space
    /// </summary>
    public float3 RadarRequest;
    public float RadarResponse;

    [GhostField] public FixedString128Bytes StdOutBuffer;

    [GhostField] public StatusLED StatusLED;
    [GhostField] public BlinkingLED NetworkReceiveLED;
    [GhostField] public BlinkingLED NetworkSendLED;
    [GhostField] public BlinkingLED RadarLED;
}
