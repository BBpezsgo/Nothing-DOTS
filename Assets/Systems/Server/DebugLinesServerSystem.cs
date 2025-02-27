using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class DebugLinesServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float now = MonoTime.Now;

        foreach (var lines in
            SystemAPI.Query<DynamicBuffer<BufferedLine>>())
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].DieAt <= now) lines.RemoveAt(i--);
            }
        }
    }
}
