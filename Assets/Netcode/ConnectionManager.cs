using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.UIElements;

public class ConnectionManager : PrivateSingleton<ConnectionManager>
{
    public static World? ClientWorld => Instance._clientWorld;
    public static World? ServerWorld => Instance._serverWorld;

    public static World ClientOrDefaultWorld => Instance._clientWorld ?? World.DefaultGameObjectInjectionWorld;
    public static World ServerOrDefaultWorld => Instance._serverWorld ?? World.DefaultGameObjectInjectionWorld;

    [SerializeField, NotNull] UIDocument? UI = default;

    World? _clientWorld = default;
    World? _serverWorld = default;

    void Start()
    {
        UI.rootVisualElement.Q<Button>("button-host").clicked += () => StartCoroutine(StartHostAsync());
        UI.rootVisualElement.Q<Button>("button-client").clicked += () => StartCoroutine(StartClientAsync());
        UI.rootVisualElement.Q<Button>("button-server").clicked += () => StartCoroutine(StartServerAsync());

        StartCoroutine(StartHostAsync());
    }

    bool ParseInput([NotNullWhen(true)] out NetworkEndpoint endpoint, [NotNullWhen(false)] out string? error)
    {
        string inputHost = UI.rootVisualElement.Q<TextField>("input-host").value;

        if (!inputHost.Contains(':'))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!ushort.TryParse(inputHost.Split(':')[1], out ushort port))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!NetworkEndpoint.TryParse(inputHost.Split(':')[0], port, out endpoint))
        {
            error = $"Invalid host input";
            return false;
        }

        error = null;
        return true;
    }

    void SetInputEnabled(bool enabled)
    {
        UI.rootVisualElement.Q<Button>("button-host").SetEnabled(enabled);
        UI.rootVisualElement.Q<Button>("button-client").SetEnabled(enabled);
        UI.rootVisualElement.Q<Button>("button-server").SetEnabled(enabled);
        UI.rootVisualElement.Q<TextField>("input-host").SetEnabled(enabled);
    }

    public IEnumerator StartHostAsync()
    {
        SetInputEnabled(false);

        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error");
        inputErrorLabel.style.display = DisplayStyle.None;
        if (!ParseInput(out NetworkEndpoint endpoint, out string? inputError))
        {
            inputErrorLabel.text = inputError;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            SetInputEnabled(true);
            yield break;
        }

        DestroyLocalWorld();

        yield return StartCoroutine(CreateServer(endpoint));

        World.DefaultGameObjectInjectionWorld ??= _serverWorld!;

        yield return StartCoroutine(CreateClient(endpoint));

        UI.gameObject.SetActive(false);
    
        SetupManager.Instance.Setup();
    }

    public IEnumerator StartClientAsync()
    {
        SetInputEnabled(false);

        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error");
        inputErrorLabel.style.display = DisplayStyle.None;
        if (!ParseInput(out NetworkEndpoint endpoint, out string? inputError))
        {
            inputErrorLabel.text = inputError;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            SetInputEnabled(true);
            yield break;
        }

        DestroyLocalWorld();

        yield return StartCoroutine(CreateClient(endpoint));

        World.DefaultGameObjectInjectionWorld ??= _clientWorld!;

        UI.gameObject.SetActive(false);
    }

    public IEnumerator StartServerAsync()
    {
        SetInputEnabled(false);

        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error");
        inputErrorLabel.style.display = DisplayStyle.None;
        if (!ParseInput(out NetworkEndpoint endpoint, out string? inputError))
        {
            inputErrorLabel.text = inputError;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            SetInputEnabled(true);
            yield break;
        }

        DestroyLocalWorld();

        yield return StartCoroutine(CreateServer(endpoint));

        World.DefaultGameObjectInjectionWorld ??= _serverWorld!;

        UI.gameObject.SetActive(false);
    }

    IEnumerator CreateServer(NetworkEndpoint endpoint)
    {
        _serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");

        SubScene[] subScenes = FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!_serverWorld.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(_serverWorld.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(_serverWorld.Unmanaged, sceneEntity))
                {
                    _serverWorld.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = _serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(endpoint);
        }
    }

    IEnumerator CreateClient(NetworkEndpoint endpoint)
    {
        _clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        SubScene[] subScenes = FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        while (!_clientWorld.IsCreated)
        {
            yield return null;
        }

        if (subScenes != null)
        {
            for (int i = 0; i < subScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParameters = new() { Flags = SceneLoadFlags.BlockOnStreamIn };
                Entity sceneEntity = SceneSystem.LoadSceneAsync(_clientWorld.Unmanaged, new Unity.Entities.Hash128(subScenes[i].SceneGUID.Value), loadParameters);
                while (!SceneSystem.IsSceneLoaded(_clientWorld.Unmanaged, sceneEntity))
                {
                    _clientWorld.Update();
                    yield return null;
                }
            }
        }

        using (EntityQuery driverQ = _clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(_clientWorld.EntityManager, endpoint);
        }
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
