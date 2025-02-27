using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static bool TryGetLocalPlayer(out Player player)
    {
        player = default;
        if (ConnectionManager.ClientOrDefaultWorld == null) return false;

        return TryGetLocalPlayer(ConnectionManager.ClientOrDefaultWorld.EntityManager, out player);
    }

    public static bool TryGetLocalPlayer(EntityManager entityManager, out Player player)
    {
        player = default;

        using EntityQuery playersQ = entityManager.CreateEntityQuery(typeof(Player));
        using EntityQuery connectionsQ = entityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton(out NetworkId networkId)) return false;

        NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ConnectionId != networkId.Value) continue;
            player = players[i];
            players.Dispose();
            return true;
        }

        players.Dispose();
        return false;
    }

    public static bool TryGetLocalPlayer(EntityManager entityManager, out Entity player)
    {
        player = default;

        using EntityQuery playersQ = entityManager.CreateEntityQuery(typeof(Player));
        using EntityQuery connectionsQ = entityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton(out NetworkId networkId)) return false;

        NativeArray<Entity> players = playersQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            var _player = entityManager.GetComponentData<Player>(players[i]);
            if (_player.ConnectionId != networkId.Value) continue;
            player = players[i];
            players.Dispose();
            return true;
        }

        players.Dispose();
        return false;
    }

    public static Player GetLocalPlayer()
    {
        using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        using EntityQuery connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton<NetworkId>(out NetworkId networkId))
        {
            throw new System.Exception("Local network id not found");
        }

        using NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ConnectionId != networkId.Value) continue;
            return players[i];
        }

        throw new System.Exception($"Player with connection id {networkId} not found");
    }
}