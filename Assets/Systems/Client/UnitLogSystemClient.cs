using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
partial class UnitLogSystemClient : SystemBase
{
    public class UnitLog
    {
        public long RequestSentAt;
        public bool IsDisposed = false;
        public readonly List<(long From, long To, byte[] Data)> Data = new();
        public long WindowStart = 0;
        public int WindowLength = 0;

        public void Dispose() => IsDisposed = true;
    }

    readonly Dictionary<SpawnedGhost, UnitLog> UnitLogs = new();

    public UnitLog GetUnitLog(SpawnedGhost ghost) => UnitLogs.TryGetValue(ghost, out UnitLog? log) ? log : (UnitLogs[ghost] = new UnitLog());

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (KeyValuePair<SpawnedGhost, UnitLog> item in UnitLogs)
        {
            if (item.Value.IsDisposed)
            {
                UnitLogs.Remove(item.Key);
                break;
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<UnitLogResponseRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            var log = GetUnitLog(command.ValueRO.Ghost);

            for (int i = 0; i < log.Data.Count; i++)
            {
                var item = log.Data[i];

                if (item.From == command.ValueRO.To + 1)
                {
                    List<byte> data = new(command.ValueRO.Data.Length + item.Data.Length);
                    data.AddRange(command.ValueRO.Data.ToArray());
                    data.AddRange(item.Data);
                    log.Data[i] = (command.ValueRO.From, item.To, data.ToArray());
                    goto ok;
                }
                else if (item.To == command.ValueRO.From - 1)
                {
                    List<byte> data = new(item.Data.Length + command.ValueRO.Data.Length);
                    data.AddRange(item.Data);
                    data.AddRange(command.ValueRO.Data.ToArray());
                    log.Data[i] = (item.From, command.ValueRO.To, data.ToArray());
                    goto ok;
                }
                else if (item.From == command.ValueRO.From)
                {
                    List<byte> data = new(Math.Max(item.Data.Length, command.ValueRO.Data.Length));
                    data.AddRange(command.ValueRO.Data.ToArray());
                    data.AddRange(item.Data[command.ValueRO.Data.Length..]);
                    log.Data[i] = (item.From, item.To, data.ToArray());
                    goto ok;
                }
                else if (item.To == command.ValueRO.To)
                {
                    List<byte> data = new(Math.Max(item.Data.Length, command.ValueRO.Data.Length));
                    data.AddRange(item.Data[..^command.ValueRO.Data.Length]);
                    data.AddRange(command.ValueRO.Data.ToArray());
                    log.Data[i] = (item.From, item.To, data.ToArray());
                    goto ok;
                }
            }

            log.Data.Add((command.ValueRO.From, command.ValueRO.To, command.ValueRO.Data.ToArray()));

        ok:;
        }

        foreach (KeyValuePair<SpawnedGhost, UnitLog> item in UnitLogs)
        {
            if (item.Value.IsDisposed) continue;
            if (MonoTime.UnixSeconds - item.Value.RequestSentAt <= 1) continue;

            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            item.Value.RequestSentAt = MonoTime.UnixSeconds;
            NetcodeUtils.CreateRPC(commandBuffer, World.Unmanaged, new UnitLogRequestRpc()
            {
                Ghost = item.Key,
                From = item.Value.WindowStart,
                Count = item.Value.WindowLength,
            });
        }
    }
}
