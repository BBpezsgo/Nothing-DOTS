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
#if UNITY_EDITOR && EDITOR_DEBUG
    [SerializeField] string DebugNickname = string.Empty;
    [SerializeField] ushort DebugPort = default;
    [SerializeField] bool AutoHost = false;
#endif
    [SerializeField, NotNull] UIDocument? UI = default;

    void Start()
    {
        UI.rootVisualElement.Q<Button>("button-host").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint, out var nickname)) return;
            StartCoroutine(StartHostAsync(endpoint, nickname));
        };
        UI.rootVisualElement.Q<Button>("button-client").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint, out var nickname)) return;
            StartCoroutine(StartClientAsync(endpoint, nickname));
        };
        UI.rootVisualElement.Q<Button>("button-server").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint, out _)) return;
            StartCoroutine(StartServerAsync(endpoint));
        };

#if UNITY_EDITOR && EDITOR_DEBUG
        if (AutoHost)
        {
            StartCoroutine(StartHostAsync(DebugPort == 0 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.Parse("127.0.0.1", DebugPort), DebugNickname));
        }
#endif
    }

    bool HandleInput([NotNullWhen(true)] out NetworkEndpoint endpoint, out FixedString32Bytes nickname)
    {
        endpoint = default;
        nickname = default;
        bool ok = true;

        Label inputErrorLabel = UI.rootVisualElement.Q<Label>("input-error-host");
        inputErrorLabel.style.display = DisplayStyle.None;

        string inputNickname = UI.rootVisualElement.Q<TextField>("input-nickname").value.Trim();

        if (inputNickname.Length >= FixedString32Bytes.UTF8MaxLengthInBytes)
        {
            inputErrorLabel.text = "Too long nickname";
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }
        else if (string.IsNullOrEmpty(inputNickname))
        {
            inputErrorLabel.text = "Empty nickname";
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }

        string inputHost = UI.rootVisualElement.Q<TextField>("input-host").value;
        if (!ParseInput(inputHost, out endpoint, out string? inputErrorHost))
        {
            inputErrorLabel.text = inputErrorHost;
            inputErrorLabel.style.display = DisplayStyle.Flex;
            ok = false;
        }
        if (ok)
        {
            return true;
        }
        else
        {
            return false;
        }
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
        UI.rootVisualElement.Q<TextField>("input-nickname").SetEnabled(enabled);
    }

    public IEnumerator StartHostAsync(NetworkEndpoint endpoint, FixedString32Bytes nickname)
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

        Debug.Log($"Set nickname to {nickname}");
        PlayerSystemClient.GetInstance(ClientWorld!.Unmanaged).SetNickname(nickname);

        SetInputEnabled(true);
        UI.gameObject.SetActive(false);

#if UNITY_EDITOR && EDITOR_DEBUG
        if (SetupManager.Instance.isActiveAndEnabled) SetupManager.Instance.Setup();
#endif
    }

    public IEnumerator StartClientAsync(NetworkEndpoint endpoint, FixedString32Bytes nickname)
    {
        SetInputEnabled(false);

        NetcodeBootstrap.DestroyLocalWorld();

        yield return StartCoroutine(NetcodeBootstrap.CreateClient(endpoint));

        World.DefaultGameObjectInjectionWorld ??= ClientWorld!;

        Debug.Log($"Set nickname to {nickname}");
        PlayerSystemClient.GetInstance(ClientWorld!.Unmanaged).SetNickname(nickname);

        SetInputEnabled(true);
        UI.gameObject.SetActive(false);
    }

    public IEnumerator StartServerAsync(NetworkEndpoint endpoint)
    {
        SetInputEnabled(false);

        NetcodeBootstrap.DestroyLocalWorld();

        yield return StartCoroutine(NetcodeBootstrap.CreateServer(endpoint));

        World.DefaultGameObjectInjectionWorld ??= ServerWorld!;

        SetInputEnabled(true);
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

    public static void DisconnectEveryone()
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
        }
    }
}
