using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;

public class UnitLogEvent : DebugEvent
{
    [JsonProperty("logType", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? LogType { get; set; } = null;

    [JsonProperty("index", DefaultValueHandling = DefaultValueHandling.Populate)]
    public uint Index { get; set; } = 0;

    [JsonProperty("timestamp", DefaultValueHandling = DefaultValueHandling.Populate)]
    public long Timestamp { get; set; } = 0;

    [JsonProperty("content", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public object? Content { get; set; } = null;

    public UnitLogEvent() : base("unitLog") { }

    public UnitLogEvent(LogPieceHeader header, object? content = null) : this()
    {
        LogType = header.Type switch
        {
            LogPieceType.Message => "Message",
            LogPieceType.CombatTurret_Shoot => "Combat Turret Shoot",
            LogPieceType.Command => "Command",
            LogPieceType.Radar => "Radar",
            LogPieceType.Transmission_WiredOut => "Transmission Send",
            LogPieceType.Transmission_WiredIn => "Transmission Receive",
            LogPieceType.Transmission_WirelessOut => "Transmission SendW",
            LogPieceType.Transmission_WirelessIn => "Transmission ReceiveW",
            LogPieceType.ProcessorSignal => "Processor Signal",
            LogPieceType.Unknown0 or LogPieceType.Unknown1 or _ => "?",
        };
        Index = header.Index;
        Timestamp = header.Timestamp;
        Content = content;
    }
}
