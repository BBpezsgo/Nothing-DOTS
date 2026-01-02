using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct PlayerSystemClient : ISystem
{
    bool _requestSent;
    SessionStatusCode _sessionStatus;
    Guid _guid;
    FixedString32Bytes _nickname;

    public static ref PlayerSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<PlayerSystemClient>();
        return ref world.GetUnsafeSystemRef<PlayerSystemClient>(handle);
    }

    void ISystem.OnCreate(ref SystemState state)
    {
        _requestSent = false;
        _guid = default;
        state.RequireForUpdate<NetworkStreamConnection>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        NetworkStreamConnection connection = SystemAPI.GetSingleton<NetworkStreamConnection>();

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SessionResponseRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            _sessionStatus = command.ValueRO.StatusCode;
            _guid = Marshal.As<FixedBytes16, Guid>(command.ValueRO.Guid);

            if (command.ValueRO.StatusCode.IsOk())
            {
                _nickname = command.ValueRO.Nickname;
                File.WriteAllBytes("session.bin", _guid.ToByteArray());
            }

            Debug.Log(string.Format("[Client] Session status: {0}\n  guid: {1}\n  nickname: {2}", _sessionStatus, _guid, _nickname));
        }

        if (connection.CurrentState != ConnectionState.State.Connected) return;

        if (!TryGetLocalPlayer(ref state, out _) && connection.CurrentState == ConnectionState.State.Connected)
        {
            if (!_requestSent)
            {
                if (_guid == default)
                {
                    if (File.Exists("session.bin"))
                    {
                        _guid = new(File.ReadAllBytes("session.bin"));

                        Debug.Log(string.Format("[Client] No player found, logging in with saved session {0}", _guid));

                        NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
                        {
                            Guid = Marshal.As<Guid, FixedBytes16>(_guid),
                        });
                    }
                    else
                    {
                        Debug.Log(string.Format("[Client] No player found, registering"));

                        NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionRegisterRequestRpc()
                        {
                            Nickname = _nickname,
                        });
                    }
                }
                else
                {
                    Debug.Log(string.Format("[Client] No player found, logging in with {0}", _guid));

                    NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
                    {
                        Guid = Marshal.As<Guid, FixedBytes16>(_guid),
                    });
                }

                _requestSent = true;
            }
        }
        else
        {
            _requestSent = false;
        }
    }

    public bool TryGetLocalPlayer(ref SystemState state, out Player player)
    {
        if (state.WorldUnmanaged.IsLocal())
        {
            return SystemAPI.TryGetSingleton<Player>(out player);
        }

        if (!SystemAPI.TryGetSingleton(out NetworkId networkId))
        {
            player = default;
            return false;
        }

        foreach (var _player in
            SystemAPI.Query<RefRO<Player>>())
        {
            if (_player.ValueRO.ConnectionId != networkId.Value) continue;
            player = _player.ValueRO;
            return true;
        }

        player = default;
        return false;
    }

    public static bool TryGetLocalPlayer(out Player player)
    {
        using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            return playersQ.TryGetSingleton<Player>(out player);
        }

        using EntityQuery connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton(out NetworkId networkId))
        {
            player = default;
            return false;
        }

        using NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ConnectionId != networkId.Value) continue;
            player = players[i];
            return true;
        }

        player = default;
        Debug.LogWarning("No local player found");
        return false;
    }

    public static bool TryGetLocalPlayer(out Entity player)
    {
        using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            return playersQ.TryGetSingletonEntity<Player>(out player);
        }

        using EntityQuery connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton(out NetworkId networkId))
        {
            player = default;
            return false;
        }

        using NativeArray<Entity> players = playersQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<Player>(players[i]).ConnectionId != networkId.Value) continue;
            player = players[i];
            return true;
        }

        player = default;
        Debug.LogWarning("No local player found");
        return false;
    }

    public void SetNickname(FixedString32Bytes nickname)
    {
        _nickname = nickname;

        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
            playersQ.GetSingletonRW<Player>().ValueRW.Nickname = nickname;
        }
    }
}
