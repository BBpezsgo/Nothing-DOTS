using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;

public class ConnectionManager : PrivateSingleton<ConnectionManager>
{
    public static World? ClientWorld => NetcodeBootstrap.ClientWorld;
    public static World? ServerWorld => NetcodeBootstrap.ServerWorld;

    public static World ClientOrDefaultWorld => NetcodeBootstrap.ClientWorld ?? World.DefaultGameObjectInjectionWorld;
    public static World ServerOrDefaultWorld => NetcodeBootstrap.ServerWorld ?? World.DefaultGameObjectInjectionWorld;
    [SerializeField] ushort DebugPort = default;
    [SerializeField, NotNull] UIDocument? UI = default;

    void Start()
    {
        UI.rootVisualElement.Q<Button>("button-host").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint)) return;
            StartCoroutine(StartHostAsync(endpoint));
        };
        UI.rootVisualElement.Q<Button>("button-client").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint)) return;
            StartCoroutine(StartClientAsync(endpoint));
        };
        UI.rootVisualElement.Q<Button>("button-server").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint)) return;
            StartCoroutine(StartServerAsync(endpoint));
        };

#if UNITY_EDITOR
        StartCoroutine(StartHostAsync(DebugPort == 0 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.Parse("127.0.0.1", DebugPort)));
#endif
    }

    bool HandleInput([NotNullWhen(true)] out NetworkEndpoint endpoint)
    {
        string inputHost = UI.rootVisualElement.Q<TextField>("input-host").value;
        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error");
        inputErrorLabel.style.display = DisplayStyle.None;
        if (!ParseInput(inputHost, out endpoint, out string? inputError))
        {
            inputErrorLabel.text = inputError;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            SetInputEnabled(true);
            return false;
        }
        return true;
    }

    bool ParseInput(
        string input,
        [NotNullWhen(true)] out NetworkEndpoint endpoint,
        [NotNullWhen(false)] out string? error)
    {
        if (!input.Contains(':'))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!ushort.TryParse(input.Split(':')[1], out ushort port))
        {
            error = $"Invalid host input";
            endpoint = default;
            return false;
        }

        if (!NetworkEndpoint.TryParse(input.Split(':')[0], port, out endpoint))
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

    public IEnumerator StartHostAsync(NetworkEndpoint endpoint)
    {
        SetInputEnabled(false);

        NetcodeBootstrap.DestroyLocalWorld();

        yield return StartCoroutine(NetcodeBootstrap.CreateServer(endpoint));

        World.DefaultGameObjectInjectionWorld ??= ServerWorld!;

        using (EntityQuery driverQ = ServerWorld!.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            endpoint = driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.GetLocalEndPoint();
        }

        yield return StartCoroutine(NetcodeBootstrap.CreateClient(endpoint));

        UI.gameObject.SetActive(false);

        SetupManager.Instance.Setup();
    }

    public IEnumerator StartClientAsync(NetworkEndpoint endpoint)
    {
        SetInputEnabled(false);

        NetcodeBootstrap.DestroyLocalWorld();

        yield return StartCoroutine(NetcodeBootstrap.CreateClient(endpoint));

        World.DefaultGameObjectInjectionWorld ??= ClientWorld!;

        UI.gameObject.SetActive(false);
    }

    public IEnumerator StartServerAsync(NetworkEndpoint endpoint)
    {
        SetInputEnabled(false);

        NetcodeBootstrap.DestroyLocalWorld();

        yield return StartCoroutine(NetcodeBootstrap.CreateServer(endpoint));

        World.DefaultGameObjectInjectionWorld ??= ServerWorld!;

        UI.gameObject.SetActive(false);
    }

    public static void KickClient(int connectionId)
    {
        if (ServerWorld == null) return;

        using EntityQuery networkIdQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        using NativeArray<Entity> entities = networkIdQ.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            NetworkId networkId = ServerWorld.EntityManager.GetComponentData<NetworkId>(entities[i]);
            if (networkId.Value != connectionId) continue;
            ServerWorld.EntityManager.AddComponentData<NetworkStreamRequestDisconnect>(entities[i], new()
            {
                Reason = NetworkStreamDisconnectReason.ClosedByRemote,
            });
        }
    }

    public static void StopServer()
    {
        if (ServerWorld != null)
        {
            using EntityQuery networkStreamConnectionQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            using EntityQuery networkIdQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            using NativeArray<Entity> entities = networkIdQ.ToEntityArray(Allocator.Temp);

            using EntityQuery driverQ = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            RefRW<NetworkStreamDriver> driver = driverQ.GetSingletonRW<NetworkStreamDriver>();

            for (int i = 0; i < entities.Length; i++)
            {
                ServerWorld.EntityManager.AddComponentData<NetworkStreamRequestDisconnect>(entities[i], new()
                {
                    Reason = NetworkStreamDisconnectReason.ClosedByRemote,
                });
            }

            NetcodeBootstrap.Stop();
        }

        if (NetcodeBootstrap.LocalWorld == null)
        {
            World.DefaultGameObjectInjectionWorld = null!;
            NetcodeBootstrap.LocalWorld = ClientServerBootstrap.CreateLocalWorld("Empty");
        }
    }
}
