using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct PlayerSystemClient : ISystem
{
    const bool SaveSessions = false;
    const string SessionsDirectoryPath = "sessions";

    bool _guidRequestSent;
    bool _sessionRequestSent;
    SessionStatusCode _sessionStatus;
    Guid _playerGuid;
    Guid _serverGuid;
    FixedString32Bytes _nickname;
    EntityQuery playersQ;
    EntityQuery connectionsQ;

    public Guid PlayerGuid => _playerGuid;

    public static ref PlayerSystemClient GetInstance(in WorldUnmanaged world) => ref world.GetSystem<PlayerSystemClient>();

    void ISystem.OnCreate(ref SystemState state)
    {
        _playerGuid = default;
        state.RequireForUpdate<NetworkStreamConnection>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        foreach (var (_, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ServerGuidResponseRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            _serverGuid = Marshal.As<FixedBytes16, Guid>(command.ValueRO.Guid);
            _guidRequestSent = false;

            Debug.Log($"{DebugEx.ClientPrefix} Server guid: `{_serverGuid}`");
        }

        foreach (var (_, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SessionResponseRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            _sessionStatus = command.ValueRO.StatusCode;
            _playerGuid = Marshal.As<FixedBytes16, Guid>(command.ValueRO.Guid);
            _sessionRequestSent = false;

            switch (command.ValueRO.StatusCode)
            {
                case SessionStatusCode.AlreadyLoggedIn:
                case SessionStatusCode.OK:
                {
                    _nickname = command.ValueRO.Nickname;
                    Debug.Log($"{DebugEx.ClientPrefix} Successfully logged in ({command.ValueRO.StatusCode})\n  Guid: {_playerGuid}\n  Nickname: {_nickname}");

                    if (!FindSavedSession(_serverGuid, out Guid savedPlayerGuid, out _) || savedPlayerGuid != _playerGuid)
                    {
                        SaveSession(_serverGuid, _playerGuid);
                    }

                    return;
                }
                case SessionStatusCode.InvalidGuid:
                {
                    Debug.Log($"{DebugEx.ClientPrefix} Invalid guid, resetting local player guid");

                    _playerGuid = default;
                    break;
                }
                default: throw new UnreachableException();
            }
        }

        NetworkStreamConnection connection = SystemAPI.GetSingleton<NetworkStreamConnection>();

        if (connection.CurrentState != ConnectionState.State.Connected) return;

        if (TryGetLocalPlayer(ref state, out _)) return;

        if (_serverGuid == default)
        {
            if (_guidRequestSent) return;
            _guidRequestSent = true;

            Debug.Log($"{DebugEx.ClientPrefix} Requesting server guid");

            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new ServerGuidRequestRpc());

            return;
        }

        if (_sessionRequestSent) return;
        _sessionRequestSent = true;

        if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        if (_playerGuid == default)
        {
            if (FindSavedSession(_serverGuid, out Guid savedPlayerGuid, out _) && _sessionStatus != SessionStatusCode.InvalidGuid)
            {
                Debug.Log($"{DebugEx.ClientPrefix} No player found, logging in with saved session\nserver: {_serverGuid}\nplayer: {savedPlayerGuid}");

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
                {
                    Guid = Marshal.As<Guid, FixedBytes16>(savedPlayerGuid),
                });
            }
            else
            {
                Debug.Log($"{DebugEx.ClientPrefix} No player found, registering");

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionRegisterRequestRpc()
                {
                    Nickname = _nickname,
                });
            }
        }
        else
        {
            Debug.Log($"{DebugEx.ClientPrefix} No player found, logging in with {_playerGuid}");

            NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
            {
                Guid = Marshal.As<Guid, FixedBytes16>(_playerGuid),
            });
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

    public bool TryGetLocalPlayer(out Player player)
    {
        if (playersQ == default) playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        if (connectionsQ == default) connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));

        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            return playersQ.TryGetSingleton<Player>(out player);
        }

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
        return false;
    }

    public bool TryGetLocalPlayer(out Entity player)
    {
        if (playersQ == default) playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        if (connectionsQ == default) connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));

        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            return playersQ.TryGetSingletonEntity<Player>(out player);
        }

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

    static bool FindSavedSession(Guid serverGuid, out Guid playerGuid, [NotNullWhen(true)] out string? file)
    {
        playerGuid = default;
        file = default;

        if (!Directory.Exists(SessionsDirectoryPath)) return false;

        foreach (string _file in Directory.GetFiles(SessionsDirectoryPath))
        {
            if (LoadSession(_file, out Guid _serverGuid, out playerGuid) && _serverGuid == serverGuid)
            {
                file = _file;
                return true;
            }
        }

        return false;
    }

    static bool LoadSession(string file, out Guid serverGuid, out Guid playerGuid)
    {
        using FileBinaryReader reader = new(file);
        try
        {
            serverGuid = reader.ReadGuid();
            playerGuid = reader.ReadGuid();
            return reader.IsEOF;
        }
        catch
        {
            serverGuid = default;
            playerGuid = default;
            return false;
        }
    }

    static string GetNonceFromIndex(uint index)
    {
        const string Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        StringBuilder result = new();
        while (true)
        {
            if (index < Chars.Length)
            {
                result.Insert(0, Chars[(int)index]);
                return result.ToString();
            }
            else
            {
                int i = (int)(index % Chars.Length);
                result.Insert(0, Chars[i]);
                index /= (uint)Chars.Length;
            }
        }
    }

    static void SaveSession(Guid serverGuid, Guid playerGuid)
    {
        if (!SaveSessions)
        {
            Debug.Log($"{DebugEx.ClientPrefix} Would save session to file");
            return;
        }

        if (!Directory.Exists(SessionsDirectoryPath))
        {
            Directory.CreateDirectory(SessionsDirectoryPath);
        }

        uint counter = 1;
        string fileName;
        while (File.Exists(fileName = Path.Combine(SessionsDirectoryPath, $"{GetNonceFromIndex(counter)}.bin")))
        {
            counter++;
            if (counter == 0) throw new Exception($"Failed to generate a session file name");
        }

        using FileBinaryWriter writer = new(fileName);
        writer.Write(serverGuid);
        writer.Write(playerGuid);

        Debug.Log($"{DebugEx.ClientPrefix} Session saved to file \"{fileName}\"");
    }

    public void OnDisconnect()
    {
        if (playersQ != default) playersQ.Dispose();
        if (connectionsQ != default) connectionsQ.Dispose();
    }
}
