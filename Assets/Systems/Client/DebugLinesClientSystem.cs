using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
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
            Segments.Batch batch = _batches[i];
            batch.Dependency.Complete();

            batch.buffer.Clear();
            for (int j = 0; j < lines.Length; j++)
            {
                batch.buffer.Add(lines[j].Value);
            }
        }
    }
}
