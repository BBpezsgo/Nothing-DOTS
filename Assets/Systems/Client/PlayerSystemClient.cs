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
    const string SessionsDirectoryPath = "sessions";

    bool GuidRequestSent;
    bool SessionRequestSent;
    SessionStatusCode SessionStatus;
    Guid PlayerGuid;
    Guid ServerGuid;
    FixedString32Bytes Nickname;

    public static ref PlayerSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<PlayerSystemClient>();
        return ref world.GetUnsafeSystemRef<PlayerSystemClient>(handle);
    }

    void ISystem.OnCreate(ref SystemState state)
    {
        SessionRequestSent = false;
        PlayerGuid = default;
        state.RequireForUpdate<NetworkStreamConnection>();
    }

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (_, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ServerGuidResponseRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            ServerGuid = Marshal.As<FixedBytes16, Guid>(command.ValueRO.Guid);

            Debug.Log(string.Format("[Client] Server guid: {0}", ServerGuid));
        }

        foreach (var (_, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SessionResponseRpc>>()
            .WithEntityAccess())
        {
            commandBuffer.DestroyEntity(entity);

            SessionStatus = command.ValueRO.StatusCode;
            PlayerGuid = Marshal.As<FixedBytes16, Guid>(command.ValueRO.Guid);

            Debug.Log(string.Format("[Client] Session status: {0}\n  guid: {1}\n  nickname: {2}", SessionStatus, PlayerGuid, Nickname));

            if (command.ValueRO.StatusCode.IsOk())
            {
                Nickname = command.ValueRO.Nickname;
                SaveSession(ServerGuid, PlayerGuid);
            }
            else if (command.ValueRO.StatusCode == SessionStatusCode.InvalidGuid)
            {
                Debug.Log(string.Format("[Client] Invalid guid, registering new player"));

                SessionRequestSent = false;
                PlayerGuid = default;
            }
        }

        NetworkStreamConnection connection = SystemAPI.GetSingleton<NetworkStreamConnection>();

        if (connection.CurrentState != ConnectionState.State.Connected) return;

        if (TryGetLocalPlayer(ref state, out _))
        {
            SessionRequestSent = false;
            GuidRequestSent = false;
            return;
        }

        if (ServerGuid == default)
        {
            if (GuidRequestSent) return;
            GuidRequestSent = true;

            Debug.Log(string.Format("[Client] Requesting server guid"));

            NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new ServerGuidRequestRpc());

            return;
        }

        if (SessionRequestSent) return;
        SessionRequestSent = true;

        if (PlayerGuid == default)
        {
            if (FindSavedSession(ServerGuid, out Guid savedPlayerGuid) && SessionStatus != SessionStatusCode.InvalidGuid)
            {
                Debug.Log(string.Format("[Client] No player found, logging in with saved session\nserver: {0}\nplayer: {1}", ServerGuid, savedPlayerGuid));

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
                {
                    Guid = Marshal.As<Guid, FixedBytes16>(savedPlayerGuid),
                });
            }
            else
            {
                Debug.Log(string.Format("[Client] No player found, registering"));

                NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionRegisterRequestRpc()
                {
                    Nickname = Nickname,
                });
            }
        }
        else
        {
            Debug.Log(string.Format("[Client] No player found, logging in with {0}", PlayerGuid));

            NetcodeUtils.CreateRPC(commandBuffer, state.WorldUnmanaged, new SessionLoginRequestRpc()
            {
                Guid = Marshal.As<Guid, FixedBytes16>(PlayerGuid),
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
        //Debug.LogWarning("No local player found");
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
        //Debug.LogWarning("No local player found");
        return false;
    }

    public void SetNickname(FixedString32Bytes nickname)
    {
        Nickname = nickname;

        if (ConnectionManager.ClientOrDefaultWorld.Unmanaged.IsLocal())
        {
            using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
            playersQ.GetSingletonRW<Player>().ValueRW.Nickname = nickname;
        }
    }

    static bool FindSavedSession(Guid serverGuid, out Guid playerGuid)
    {
        playerGuid = default;

        if (!Directory.Exists(SessionsDirectoryPath)) return false;

        foreach (string file in Directory.GetFiles(SessionsDirectoryPath))
        {
            if (LoadSession(file, out Guid _serverGuid, out playerGuid) && _serverGuid == serverGuid) return true;
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

    static void SaveSession(Guid serverGuid, Guid playerGuid)
    {
        if (!Directory.Exists(SessionsDirectoryPath))
        {
            Directory.CreateDirectory(SessionsDirectoryPath);
        }

        uint counter = 1;
        string fileName;
        while (File.Exists(fileName = Path.Combine(SessionsDirectoryPath, $"{counter}.bin")))
        {
            counter++;
            if (counter == 0) throw new Exception($"Failed to generate a session file name");
        }

        using FileBinaryWriter writer = new(fileName);
        writer.Write(serverGuid);
        writer.Write(playerGuid);
    }
}
