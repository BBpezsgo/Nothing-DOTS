using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class WorldLabelSystemServerSystem : SystemBase
{
    Random _random;

    protected override void OnCreate()
    {
        _random = Random.CreateFromIndex(69);
        RequireForUpdate<WorldLabels>();
    }

    protected override void OnUpdate()
    {
        DynamicBuffer<BufferedWorldLabel> labels = SystemAPI.GetSingletonBuffer<BufferedWorldLabel>();
        float now = MonoTime.Now;

        // float3 min = new(-100f, 0f, -100f);
        // float3 max = new(100f, 10f, 100f);
        // if (labels.Length < 500)
        // {
        //     labels.Add(new BufferedWorldLabel(_random.NextFloat3(min, max), 0b101, "What", now + 1f));
        // }

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].DieAt <= now) labels.RemoveAt(i--);
        }
    }
}
