using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct NetworkEventsSystemClient : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        NativeArray<NetCodeConnectionEvent>.ReadOnly v = SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick;
        for (int i = 0; i < v.Length; i++)
        {
            NetCodeConnectionEvent e = v[i];

            ConnectionManager.Instance.OnNetworkEventClient(e);

            ChatManager.Instance.AppendChatMessageElement(-1, (string?)(e.State switch
            {
                ConnectionState.State.Disconnected => $"Disconnected: {e.DisconnectReason}",
                ConnectionState.State.Connecting => $"Connecting ...",
                ConnectionState.State.Handshake => $"Handshaking ...",
                ConnectionState.State.Approval => $"Waiting for approval ...",
                ConnectionState.State.Connected => $"Connected",
                ConnectionState.State.Unknown or _ => e.ToFixedString().ToString(),
            }), DateTimeOffset.UtcNow);

            if (e.State == ConnectionState.State.Disconnected)
            {
                Debug.Log($"{DebugEx.ClientPrefix} Clearing system states ...");

                state.WorldUnmanaged.GetUnsafeSystemRef<BuildingsSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<BuildingsSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<DebugLinesClientSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<DebugLinesClientSystem>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<PlayerPositionSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<PlayerPositionSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<PlayerSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<PlayerSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<ProjectileSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<ProjectileSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<ProcessorSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<ProcessorSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<ResearchSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<ResearchSystemClient>()).OnDisconnect();
                state.WorldUnmanaged.GetUnsafeSystemRef<UnitsSystemClient>(state.WorldUnmanaged.GetExistingUnmanagedSystem<UnitsSystemClient>()).OnDisconnect();

                state.World.GetExistingSystemManaged<CompilerSystemClient>().OnDisconnect();
                state.World.GetExistingSystemManaged<EntityInfoUISystemClient>().OnDisconnect();
                state.World.GetExistingSystemManaged<VisualEffectSystemClient>().OnDisconnect();
                state.World.GetExistingSystemManaged<WireRendererSystemClient>().OnDisconnect();
                state.World.GetExistingSystemManaged<WorldLabelSystemClientSystem>().OnDisconnect();
            }
        }
    }
}
