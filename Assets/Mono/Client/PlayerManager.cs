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

        player = default;

        using EntityQuery playersQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Player));
        using EntityQuery connectionsQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
        if (!connectionsQ.TryGetSingleton(out NetworkId networkId)) return false;

        using NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ConnectionId != networkId.Value) continue;
            player = players[i];
            return true;
        }

        return false;
    }
}