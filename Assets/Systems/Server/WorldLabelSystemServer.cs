using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class WorldLabelSystemServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float now = MonoTime.Now;

        foreach (var labels in
            SystemAPI.Query<DynamicBuffer<BufferedWorldLabel>>())
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].DieAt <= now) labels.RemoveAt(i--);
            }
        }
    }
}
