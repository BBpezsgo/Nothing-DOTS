using System.Diagnostics.CodeAnalysis;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DebugLinesClientSystem : SystemBase
{
    [NotNull] Segments.Batch[]? _batches = default;

    protected override void OnCreate()
    {
        RequireForUpdate<DebugLines>();
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
        DynamicBuffer<BufferedLine> lines = SystemAPI.GetSingletonBuffer<BufferedLine>(true);

        for (int i = 0; i < _batches.Length; i++)
        {
            _batches[i].Dependency.Complete();
            _batches[i].buffer.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            _batches[lines[i].Color - 1].buffer.Add(lines[i].Value);
        }
    }
}
