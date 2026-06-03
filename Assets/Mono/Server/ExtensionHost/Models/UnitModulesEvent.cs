using System;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;

public class UnitModuleField
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("offset")] public int Offset { get; set; }

    public unsafe UnitModuleField(string name, string type, void* address, void* baseAddress) : this(name, type, (int)((nint)address - (nint)baseAddress))
    {

    }

    public UnitModuleField(string name, string type, int offset)
    {
        Name = name;
        Type = type;
        Offset = offset;
    }
}

public class UnitModule
{
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("fields")] public UnitModuleField[] Fields { get; set; }

    public UnitModule(string type, UnitModuleField[] fields)
    {
        Type = type;
        Fields = fields;
    }
}

public class UnitModulesEvent : DebugEvent
{
    [JsonProperty("modules")] public UnitModule[] Modules { get; set; }

    public UnitModulesEvent(UnitModule[] modules) : base("unitModules")
    {
        Modules = modules;
    }
}
