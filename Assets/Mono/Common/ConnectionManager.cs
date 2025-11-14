using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;

public class ConnectionManager : Singleton<ConnectionManager>
{
    public static World? ClientWorld => NetcodeBootstrap.ClientWorld;
    public static World? ServerWorld => NetcodeBootstrap.ServerWorld;
    public static World? LocalWorld => NetcodeBootstrap.LocalWorld;

    public static World ClientOrDefaultWorld => NetcodeBootstrap.ClientWorld ?? NetcodeBootstrap.LocalWorld ?? World.DefaultGameObjectInjectionWorld;
    public static World ServerOrDefaultWorld => NetcodeBootstrap.ServerWorld ?? NetcodeBootstrap.LocalWorld ?? World.DefaultGameObjectInjectionWorld;
#if UNITY_EDITOR && EDITOR_DEBUG
    [SerializeField] string DebugNickname = string.Empty;
    [SerializeField] ushort DebugPort = default;
    [SerializeField] bool AutoHost = false;
    [SerializeField] bool Singleplayer = false;
    [SerializeField] bool NoClient = false;
#endif

    [SerializeField, NotNull] GameObject? ServerObjects = default;
    [SerializeField, NotNull] GameObject? ClientObjects = default;
    [SerializeField, NotNull] UIDocument? UI = default;

    void Start()
    {
        UI.rootVisualElement.Q<Button>("button-host").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint, out FixedString32Bytes nickname)) return;
            StartCoroutine(StartHostAsync(endpoint, nickname));
        };
        UI.rootVisualElement.Q<Button>("button-client").clicked += () =>
        {
            if (!HandleInput(out NetworkEndpoint endpoint, out FixedString32Bytes nickname)) return;
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
            if (Singleplayer)
            {
                StartCoroutine(StartSingleplayerAsync(DebugNickname));
            }
            else if (NoClient)
            {
                StartCoroutine(StartServerAsync(DebugPort == 0 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.Parse("127.0.0.1", DebugPort)));
            }
            else
            {
                StartCoroutine(StartHostAsync(DebugPort == 0 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.Parse("127.0.0.1", DebugPort), DebugNickname));
            }
        }
#endif
    }

    bool HandleInput([NotNullWhen(true)] out NetworkEndpoint endpoint, out FixedString32Bytes nickname)
    {
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

        nickname = inputNickname;

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

    public void OnNetworkEvent(NetCodeConnectionEvent e)
    {
        string? text = e.State switch
        {
            ConnectionState.State.Unknown => null,
            ConnectionState.State.Disconnected => e.DisconnectReason.ToString(),
            ConnectionState.State.Connecting => $"Connecting ...",
            ConnectionState.State.Handshake => $"Handshaking ...",
            ConnectionState.State.Approval => $"Approval ...",
            ConnectionState.State.Connected => null,
            _ => throw new UnreachableException(),
        };

        if (UI.enabled = text is not null)
        {
            UIManager.Instance.CloseAllUI(UI);
            Label label = UI.rootVisualElement.Q<Label>("label-status");
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (e.State == ConnectionState.State.Disconnected)
        {
            ServerObjects.SetActive(false);
            ClientObjects.SetActive(false);
            SetInputEnabled(true);
        }
    }

    void SetInputEnabled(bool enabled)
    {
        UI.rootVisualElement.Q<Button>("button-host").SetEnabled(enabled);
        UI.rootVisualElement.Q<Button>("button-client").SetEnabled(enabled);
        UI.rootVisualElement.Q<Button>("button-server").SetEnabled(enabled);
        UI.rootVisualElement.Q<TextField>("input-host").SetEnabled(enabled);
        UI.rootVisualElement.Q<TextField>("input-nickname").SetEnabled(enabled);
    }

    public IEnumerator StartSingleplayerAsync(FixedString32Bytes nickname)
    {
        Debug.Log($"START SINGLEPLAYER");

        SetInputEnabled(false);

        Debug.Log($" -> NetcodeBootstrap.DestroyLocalWorld");
        NetcodeBootstrap.DestroyLocalWorld();

        Debug.Log($" -> CreateLocal");
        yield return StartCoroutine(NetcodeBootstrap.CreateLocal());

        Debug.Log($" -> DefaultGameObjectInjectionWorld");
        World.DefaultGameObjectInjectionWorld ??= NetcodeBootstrap.LocalWorld!;

        Debug.Log($" -> EnablingObjects");
        ServerObjects.SetActive(true);
        ClientObjects.SetActive(true);
        yield return new WaitForEndOfFrame();

        Debug.Log($" -> Set nickname to {nickname}");
        PlayerSystemClient.GetInstance(LocalWorld!.Unmanaged).SetNickname(nickname);

        Debug.Log($" -> Disabling UI");
        UI.enabled = false;

#if UNITY_EDITOR && EDITOR_DEBUG
        if (SetupManager.Instance.isActiveAndEnabled) SetupManager.Instance.Setup();
#endif
    }

    public IEnumerator StartHostAsync(NetworkEndpoint endpoint, FixedString32Bytes nickname)
    {
        Debug.Log($"START HOST ({endpoint})");

        SetInputEnabled(false);

        Debug.Log($" -> NetcodeBootstrap.DestroyLocalWorld");
        NetcodeBootstrap.DestroyLocalWorld();

        Debug.Log($" -> NetcodeBootstrap.CreateServer({endpoint})");
        yield return StartCoroutine(NetcodeBootstrap.CreateServer(endpoint));

        Debug.Log($" -> DefaultGameObjectInjectionWorld");
        World.DefaultGameObjectInjectionWorld ??= ServerWorld!;

        Debug.Log($" -> EnablingObjects");
        ServerObjects.SetActive(true);
        ClientObjects.SetActive(true);
        yield return new WaitForEndOfFrame();

        using (EntityQuery driverQ = ServerWorld!.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
        {
            endpoint = driverQ.GetSingletonRW<NetworkStreamDriver>().ValueRW.GetLocalEndPoint();
        }

        Debug.Log($" -> endpoint = {endpoint}");

        Debug.Log($" -> NetcodeBootstrap.CreateClient({endpoint})");
        yield return StartCoroutine(NetcodeBootstrap.CreateClient(endpoint));

        Debug.Log($" -> Set nickname to {nickname}");
        PlayerSystemClient.GetInstance(ClientWorld!.Unmanaged).SetNickname(nickname);

#if UNITY_EDITOR && EDITOR_DEBUG
        if (SetupManager.Instance.isActiveAndEnabled) SetupManager.Instance.Setup();
#endif
    }

    public IEnumerator StartClientAsync(NetworkEndpoint endpoint, FixedString32Bytes nickname)
    {
        Debug.Log($"START CLIENT ({endpoint})");

        SetInputEnabled(false);

        Debug.Log($" -> NetcodeBootstrap.DestroyLocalWorld");
        NetcodeBootstrap.DestroyLocalWorld();

        Debug.Log($" -> NetcodeBootstrap.CreateClient({endpoint})");
        yield return StartCoroutine(NetcodeBootstrap.CreateClient(endpoint));

        Debug.Log($" -> DefaultGameObjectInjectionWorld");
        World.DefaultGameObjectInjectionWorld ??= ClientWorld!;

        Debug.Log($" -> EnablingObjects");
        ServerObjects.SetActive(false);
        ClientObjects.SetActive(true);
        yield return new WaitForEndOfFrame();

        Debug.Log($" -> Set nickname to {nickname}");
        PlayerSystemClient.GetInstance(ClientWorld!.Unmanaged).SetNickname(nickname);
    }

    public IEnumerator StartServerAsync(NetworkEndpoint endpoint)
    {
        Debug.Log($"START SERVER ({endpoint})");

        SetInputEnabled(false);

        Debug.Log($" -> NetcodeBootstrap.DestroyLocalWorld");
        NetcodeBootstrap.DestroyLocalWorld();

        Debug.Log($" -> NetcodeBootstrap.CreateServer({endpoint})");
        yield return StartCoroutine(NetcodeBootstrap.CreateServer(endpoint));

        Debug.Log($" -> DefaultGameObjectInjectionWorld");
        World.DefaultGameObjectInjectionWorld ??= ServerWorld!;

        Debug.Log($" -> EnablingObjects");
        ServerObjects.SetActive(true);
        ClientObjects.SetActive(false);
        yield return new WaitForEndOfFrame();

        Debug.Log($" -> Disabling UI");
        UI.enabled = false;

#if UNITY_EDITOR && EDITOR_DEBUG
        Debug.Log($" -> SetupManager.Instance.Setup()");
        if (SetupManager.Instance.isActiveAndEnabled) SetupManager.Instance.Setup();
#endif
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
        if (ServerWorld == null) return;

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
