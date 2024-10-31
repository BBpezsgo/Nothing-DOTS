using System.Diagnostics.CodeAnalysis;
using Unity.Entities;

public class EntityInfoUIReference : ICleanupComponentData
{
    [NotNull] public EntityInfoUI? Value = default;
}
