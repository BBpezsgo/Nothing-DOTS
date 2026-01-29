using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct NetworkEventsSystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        NativeArray<NetCodeConnectionEvent>.ReadOnly v = SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick;
        for (int i = 0; i < v.Length; i++)
        {
            NetCodeConnectionEvent e = v[i];
            ConnectionManager.Instance.OnNetworkEvent(e);
            ChatManager.Instance.AppendChatMessageElement(-1, (string?)(e.State switch
            {
                ConnectionState.State.Disconnected => $"Disconnected: {e.DisconnectReason}",
                ConnectionState.State.Connecting => $"Connecting ...",
                ConnectionState.State.Handshake => $"Handshaking ...",
                ConnectionState.State.Approval => $"Waiting for approval ...",
                ConnectionState.State.Connected => $"Connected",
                ConnectionState.State.Unknown or _ => e.ToFixedString().ToString(),
            }), DateTimeOffset.UtcNow);
        }
    }
}
