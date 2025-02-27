using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DebugLinesClientSystem : SystemBase
{
    [NotNull] Segments.Batch[]? _batches = default;

    protected override void OnCreate()
    {
        RequireForUpdate<DebugLinesSettings>();
    }

    protected override void OnStartRunning()
    {
        DebugLinesSettings settings = SystemAPI.ManagedAPI.GetSingleton<DebugLinesSettings>();
        _batches = new Segments.Batch[settings.Materials.Length];
        for (int i = 0; i < settings.Materials.Length; i++)
        {
            Segments.Core.CreateBatch(out _batches[i], settings.Materials[i]);
        }
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton(out NetworkId networkId)) return;

        foreach (var (player, lines) in
            SystemAPI.Query<RefRO<Player>, DynamicBuffer<BufferedLine>>())
        {
            if (player.ValueRO.ConnectionId != networkId.Value) continue;

            for (int i = 0; i < _batches.Length; i++)
            {
                _batches[i].Dependency.Complete();
                _batches[i].buffer.Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                _batches[lines[i].Color - 1].buffer.Add(lines[i].Value);
            }
            break;
        }
    }
}
