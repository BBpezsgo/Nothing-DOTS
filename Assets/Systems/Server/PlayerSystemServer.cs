using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct PlayerSystemServer : ISystem
{
    int _teamCounter;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PrefabDatabase>();
        _teamCounter = 0;
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        PrefabDatabase prefabs = SystemAPI.GetSingleton<PrefabDatabase>();
        var spawns = SystemAPI.GetSingletonBuffer<BufferedSpawn>(false);

        foreach (var (id, entity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<InitializedClient>()
            .WithEntityAccess())
        {
            Debug.Log(string.Format("[Server] Client {0} initialized", id.ValueRO.Value));
            commandBuffer.AddComponent<InitializedClient>(entity);
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SessionRegisterRequestRpc>>()
            .WithEntityAccess())
        {
            var source = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);
            commandBuffer.DestroyEntity(entity);

            Debug.Log(string.Format("[Server] Received register request from client {0}", source.ValueRO.Value));

            (bool, Player) exists = default;

            foreach (var player in
                SystemAPI.Query<RefRO<Player>>())
            {
                if (player.ValueRO.ConnectionId == source.ValueRO.Value)
                {
                    exists = (true, player.ValueRO);
                }
            }

            if (exists.Item1)
            {
                Debug.LogWarning("[Server] Already logged in");
                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<SessionResponseRpc>(response, new()
                {
                    StatusCode = SessionStatusCode.AlreadyLoggedIn,
                    Nickname = exists.Item2.Nickname,
                    Guid = default,
                });
            }
            else
            {
                Guid guid;
                unsafe
                {
                    ReadOnlySpan<byte> bytes = stackalloc byte[16];
                    byte* ptr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(bytes));
                    *(int*)(ptr + 0) = source.ValueRO.Value; // 4
                    *(double*)(ptr + 4) = SystemAPI.Time.ElapsedTime; // 8
                    *(uint*)(ptr + 12) = 0x69420; // 4
                    guid = new Guid(bytes);
                }

                Entity newPlayer = commandBuffer.Instantiate(prefabs.Player);
                commandBuffer.SetComponent<Player>(newPlayer, new()
                {
                    ConnectionId = source.ValueRO.Value,
                    ConnectionState = PlayerConnectionState.Connected,
                    Team = -1,
                    IsCoreComputerSpawned = false,
                    Resources = 5,
                    Guid = guid,
                    Nickname = command.ValueRO.Nickname,
                });

                Debug.Log("[Server] Player created");
                ChatSystemServer.SendChatMessage(commandBuffer, string.Format("Player {0} connected", command.ValueRO.Nickname));

                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<SessionResponseRpc>(response, new()
                {
                    StatusCode = SessionStatusCode.OK,
                    Guid = Marshal.As<Guid, FixedBytes16>(ref guid),
                    Nickname = command.ValueRO.Nickname,
                });
            }
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SessionLoginRequestRpc>>()
            .WithEntityAccess())
        {
            var source = SystemAPI.GetComponentRO<NetworkId>(request.ValueRO.SourceConnection);
            commandBuffer.DestroyEntity(entity);

            FixedBytes16 guid = command.ValueRO.Guid;

            Debug.Log(string.Format("[Server] Received login request from client {0} with guid {1}", source.ValueRO.Value, Marshal.As<FixedBytes16, Guid>(guid)));

            bool exists = false;
            foreach (var player in
                SystemAPI.Query<RefRW<Player>>())
            {
                if (player.ValueRO.Guid != Marshal.As<FixedBytes16, Guid>(guid)) continue;

                exists = true;
                bool loggedIn = player.ValueRO.ConnectionId != -1;
                if (!loggedIn)
                {
                    player.ValueRW.ConnectionId = source.ValueRO.Value;
                    player.ValueRW.ConnectionState = PlayerConnectionState.Connected;
                    ChatSystemServer.SendChatMessage(commandBuffer, string.Format("Player {0} reconnected", player.ValueRO.Nickname));
                }

                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<SessionResponseRpc>(response, new()
                {
                    StatusCode = loggedIn ? SessionStatusCode.AlreadyLoggedIn : SessionStatusCode.OK,
                    Guid = guid,
                    Nickname = player.ValueRO.Nickname,
                });
                break;
            }

            if (!exists)
            {
                Entity response = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SendRpcCommandRequest>(response, new()
                {
                    TargetConnection = request.ValueRO.SourceConnection,
                });
                commandBuffer.AddComponent<SessionResponseRpc>(response, new()
                {
                    StatusCode = SessionStatusCode.InvalidGuid,
                    Guid = guid,
                    Nickname = default,
                });
            }
        }

        foreach (var player in
            SystemAPI.Query<RefRW<Player>>())
        {
            if (player.ValueRO.Team == -1)
            {
                player.ValueRW.Team = _teamCounter++;
            }

            if (player.ValueRO.ConnectionState != PlayerConnectionState.Connected) continue;

            bool found = false;
            foreach (var id in
                SystemAPI.Query<RefRO<NetworkId>>()
                .WithAll<InitializedClient>())
            {
                if (id.ValueRO.Value == player.ValueRO.ConnectionId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.Log(string.Format("[Server] Client {0} disconnected", player.ValueRO.ConnectionId));
                ChatSystemServer.SendChatMessage(commandBuffer, string.Format("Player {0} disconnected", player.ValueRO.Nickname));

                player.ValueRW.ConnectionId = -1;
                player.ValueRW.ConnectionState = PlayerConnectionState.Disconnected;
            }
            else
            {
                if (!player.ValueRO.IsCoreComputerSpawned)
                {
                    for (int i = 0; i < spawns.Length; i++)
                    {
                        if (spawns[i].IsOccupied) continue;
                        spawns[i] = spawns[i] with { IsOccupied = true };

                        Entity coreComputer = commandBuffer.Instantiate(prefabs.CoreComputer);
                        commandBuffer.SetComponent<UnitTeam>(coreComputer, new()
                        {
                            Team = player.ValueRO.Team
                        });
                        commandBuffer.SetComponent<LocalTransform>(coreComputer, LocalTransform.FromPosition(spawns[i].Position));
                        commandBuffer.SetComponent<GhostOwner>(coreComputer, new()
                        {
                            NetworkId = player.ValueRO.ConnectionId,
                        });

                        Entity builder = commandBuffer.Instantiate(prefabs.Builder);
                        commandBuffer.SetComponent<UnitTeam>(builder, new()
                        {
                            Team = player.ValueRO.Team
                        });
                        commandBuffer.SetComponent<LocalTransform>(builder, LocalTransform.FromPosition(spawns[i].Position + new Unity.Mathematics.float3(2f, 0.5f, 2f)));
                        commandBuffer.SetComponent<GhostOwner>(builder, new()
                        {
                            NetworkId = player.ValueRO.ConnectionId,
                        });

                        break;
                    }
                    player.ValueRW.IsCoreComputerSpawned = true;
                }
            }
        }
    }
}
