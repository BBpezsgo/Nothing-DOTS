using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;

class ExtensionAuthenticator : MonoBehaviour
{
    TcpListenerManager? Listener;
    JsonRpcServer? Server;
    CancellationTokenSource? CancellationTokenSource;

    void OnEnable()
    {
        CancellationTokenSource = new CancellationTokenSource();
    }

    void Update()
    {
        if (Listener is null || (Listener.ConnectedClient is null && !Listener.IsListening))
        {
            Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Disposing old TCP listener, creating new one");
            Listener?.Dispose();
            Listener = TcpListenerManager.Listen(IPAddress.Parse("127.0.0.1"), 8051, "ExtClient");
        }

        if (Listener.ConnectedClient is null && Server is not null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Disposing server");
            Server?.Dispose();
            Server = null;
        }

        if (Listener.ConnectedClient is not null && Server is null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Creating server");

            NetworkStream stream = Listener.ConnectedClient.GetStream();
            Server = JsonRpcServer.Create(options =>
            {
                options.WithInput(stream);
                options.WithOutput(stream);

                options.OnRequest("authenticate", () => MainThreadManager.Instance.ScheduleAsync(() =>
                {
                    return PlayerSystemClient.GetInstance(ConnectionManager.ClientOrDefaultWorld.Unmanaged).PlayerGuid.ToString();
                }));
            });

            _ = Server.Initialize(CancellationTokenSource?.Token ?? CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.IsCanceled)
                    {
                        Debug.LogWarning($"{DebugEx.ServerPrefix} [ExtClient] Server initialization canceled");
                    }
                    else if (task.IsFaulted)
                    {
                        Debug.LogError($"{DebugEx.ServerPrefix} [ExtClient] Server initialization failed");
                        Debug.LogException(task.Exception);
                    }
                    else
                    {
                        Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Server initialized");
                    }
                }, TaskScheduler.Default);
        }
    }

    void OnDisable() => Dispose();
    void OnDestroy() => Dispose();

    void Dispose()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource = null;

        if (Server is not null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Disposing server");
            Server.Dispose();
            Server = null;
        }

        if (Listener is not null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [ExtClient] Disposing TCP listener");
            Listener.Dispose();
            Listener = null;
        }
    }
}
