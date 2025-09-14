using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct DebugLinesClientSystem : ISystem
{
    NativeArray<Entity>.ReadOnly Batches;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DebugLinesSettings>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton(out NetworkId networkId)) return;

        if (!Batches.IsCreated)
        {
            DebugLinesSettings settings = SystemAPI.ManagedAPI.GetSingleton<DebugLinesSettings>();
            NativeArray<Entity> _batches = new(settings.Materials.Length, Allocator.Persistent);
            for (int i = 0; i < settings.Materials.Length; i++)
            {
                Segments.Core.Create(out Entity v, settings.Materials[i]);
                _batches[i] = v;
            }
            Batches = _batches.AsReadOnly();
        }

        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        NativeArray<DynamicBuffer<Unity.Mathematics.float3x2>> batches = new(Batches.Length, Allocator.Temp);

        for (int i = 0; i < Batches.Length; i++)
        {
            batches[i] = Segments.Core.GetBuffer(Batches[i], false);
        }

        foreach (var (player, lines) in
            SystemAPI.Query<RefRO<Player>, DynamicBuffer<BufferedLine>>())
        {
            if (player.ValueRO.ConnectionId != networkId.Value) continue;

            foreach (var (_, command, entity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<DebugLineRpc>>()
                .WithEntityAccess())
            {
                commandBuffer.DestroyEntity(entity);
                lines.Add(new BufferedLine()
                {
                    Value = command.ValueRO.Position,
                    Color = command.ValueRO.Color,
                    DieAt = (float)SystemAPI.Time.ElapsedTime + 0.5f,
                });
            }

            for (int i = 0; i < batches.Length; i++)
            {
                batches[i].Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].DieAt <= SystemAPI.Time.ElapsedTime) lines.RemoveAt(i--);
                else batches[lines[i].Color - 1].Add(lines[i].Value);
            }
            break;
        }
    }
}
