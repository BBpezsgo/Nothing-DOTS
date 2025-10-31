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
            switch (e.State)
            {
                case ConnectionState.State.Disconnected:
                    ChatManager.Instance.AppendChatMessageElement(-1, $"Disconnected: {e.DisconnectReason}");
                    break;
                case ConnectionState.State.Connecting:
                    ChatManager.Instance.AppendChatMessageElement(-1, $"Connecting ...");
                    break;
                case ConnectionState.State.Handshake:
                    ChatManager.Instance.AppendChatMessageElement(-1, $"Handshaking ...");
                    break;
                case ConnectionState.State.Approval:
                    ChatManager.Instance.AppendChatMessageElement(-1, $"Waiting for approval ...");
                    break;
                case ConnectionState.State.Connected:
                    ChatManager.Instance.AppendChatMessageElement(-1, $"Connected");
                    break;
                case ConnectionState.State.Unknown:
                default:
                    ChatManager.Instance.AppendChatMessageElement(-1, e.ToFixedString().ToString());
                    break;
            }
        }
    }
}
