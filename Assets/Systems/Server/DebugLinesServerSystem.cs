using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class DebugLinesServerSystem : SystemBase
{
    Random _random;

    protected override void OnCreate()
    {
        RequireForUpdate<DebugLines>();
        _random = Random.CreateFromIndex(69);
    }

    protected override void OnUpdate()
    {
        DynamicBuffer<BufferedLine> lines = SystemAPI.GetSingletonBuffer<BufferedLine>();
        float now = MonoTime.Now;
        float3 min = new(-100f, 0f, -100f);
        float3 max = new(100f, 10f, 100f);

        // if (lines.Length < 500)
        // {
        //     lines.Add(new BufferedLine(new float3x2(
        //         _random.NextFloat3(min, max),
        //         _random.NextFloat3(min, max)
        //     ), now + 1f));
        // }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].DieAt <= now) lines.RemoveAt(i--);
        }
    }
}
