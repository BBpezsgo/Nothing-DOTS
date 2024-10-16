using System.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

public class ConnectionManager : PrivateSingleton<ConnectionManager>
{
    public static ConnectionState Status
    {
        get
        {
            using EntityQuery driverQ = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConnectionState>());
            return driverQ.GetSingletonRW<ConnectionState>().ValueRO;
        }
    }
    public static World? ServerWorld => Instance._serverWorld;
    public static World? ClientWorld => Instance._clientWorld;

    [SerializeField] UIDocument UI;

    World? _clientWorld;
    World? _serverWorld;

    void Start()
    {
        UI.rootVisualElement.Q<Button>("button-host").clicked += () => StartCoroutine(StartHostAsync());

        UI.rootVisualElement.Q<Button>("button-client").SetEnabled(false);
        UI.rootVisualElement.Q<Button>("button-server").SetEnabled(false);
    }

    public IEnumerator StartHostAsync()
    {
        string inputHost = UI.rootVisualElement.Q<TextField>("input-host").value;
        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error");
        inputErrorLabel.visible = false;

        if (!inputHost.Contains(':'))
        {
            inputErrorLabel.text = $"Invalid host input";
            inputErrorLabel.visible = true;
            yield break;
        }

        if (!ushort.TryParse(inputHost.Split(':')[1], out var port))
        {
            inputErrorLabel.text = $"Invalid host input";
            inputErrorLabel.visible = true;
            yield break;
        }

        if (!NetworkEndpoint.TryParse(inputHost.Split(':')[0], port, out var endpoint))
        {
            inputErrorLabel.text = $"Invalid host input";
            inputErrorLabel.visible = true;
            yield break;
        }

        UI.rootVisualElement.Q<Button>("button-host").SetEnabled(false);

        World server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        World client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        DestroyLocalWorld();
        World.DefaultGameObjectInjectionWorld ??= server;
        _serverWorld = server;
        _clientWorld = client;

        var subScenes = FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!server.IsCreated || !client.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(server.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(server.Unmanaged, sceneEntity))
                {
                    server.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(endpoint);
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(client.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(client.Unmanaged, sceneEntity))
                {
                    client.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, endpoint);
        }

        UI.enabled = false;
    }

    void DestroyLocalWorld()
    {
        foreach (World world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }
    }
}
