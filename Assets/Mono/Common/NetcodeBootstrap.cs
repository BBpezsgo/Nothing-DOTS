using System.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
class NetcodeBootstrap : ClientServerBootstrap
{
    public static new World? ServerWorld = default;
    public static new World? ClientWorld = default;
    public static World? LocalWorld = default;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        ServerWorld = null;
        ClientWorld = null;
        LocalWorld = null;
    }

    public static void DestroyLocalWorld()
    {
        LocalWorld?.Dispose();
        foreach (World world in World.All)
        {
            if (world == LocalWorld) continue;
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }
        LocalWorld = null;
    }

    public static IEnumerator CreateLocal()
    {
        LocalWorld = CreateLocalWorld("LocalWorld");

        SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!LocalWorld.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(LocalWorld.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(LocalWorld.Unmanaged, sceneEntity))
                {
                    LocalWorld.Update();
                    yield return null;
                }
            }
        }

        Debug.Log($"1");
        using (EntityQuery prefabsQ = LocalWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PrefabDatabase>()))
        {
            Debug.Log($"2");
            if (prefabsQ.TryGetSingleton<PrefabDatabase>(out PrefabDatabase prefabs))
            {
                Debug.Log($"3");
                Debug.Log($"Local player created");
                Entity newPlayer = LocalWorld.EntityManager.Instantiate(prefabs.Player);
                LocalWorld.EntityManager.SetComponentData<Player>(newPlayer, new()
                {
                    ConnectionId = 0,
                    ConnectionState = PlayerConnectionState.Local,
                    Team = -1,
                });
            }
            else
            {
                Debug.Log($"4");
            }
        }
    }

    public static IEnumerator CreateServer(NetworkEndpoint endpoint)
    {
        ServerWorld = CreateServerWorld("ServerWorld");

        SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!ServerWorld.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(ServerWorld.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(ServerWorld.Unmanaged, sceneEntity))
                {
                    ServerWorld.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(endpoint);
        }

        using (EntityQuery prefabsQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PrefabDatabase>()))
        {
            if (prefabsQ.TryGetSingleton<PrefabDatabase>(out PrefabDatabase prefabs))
            {
                Debug.Log($"Local player created");
                Entity newPlayer = ServerWorld.EntityManager.Instantiate(prefabs.Player);
                ServerWorld.EntityManager.SetComponentData<Player>(newPlayer, new()
                {
                    ConnectionId = 0,
                    ConnectionState = PlayerConnectionState.Server,
                    Team = -1,
                });
            }
        }
    }

    public static IEnumerator CreateClient(NetworkEndpoint endpoint)
    {
        ClientWorld = CreateClientWorld("ClientWorld");

        SubScene[] subScenes = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!ClientWorld.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(ClientWorld.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(ClientWorld.Unmanaged, sceneEntity))
                {
                    ClientWorld.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientWorld.EntityManager, endpoint);
        }
    }

    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 0;
        LocalWorld = CreateLocalWorld(defaultWorldName);
        return true;
    }
}
