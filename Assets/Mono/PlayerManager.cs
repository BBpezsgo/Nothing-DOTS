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

        using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        using EntityQuery connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
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