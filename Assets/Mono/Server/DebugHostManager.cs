using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using UnityEngine;

class DebugHostManager : MonoBehaviour
{
    TcpListenerManager? Listener;
    readonly List<(DebugHost Host, TcpListenerManager Listener)> Hosts = new();
    readonly ConcurrentQueue<(Func<Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.ResponseBody> Task, IRequestResponder RequestResponder)> PendingRequests = new();

    class UnityLogger : Logger
    {
        public override void Trace(string? value) => UnityEngine.Debug.Log($"{DebugEx.ServerPrefix} [DAP] {value}");
        public override void Debug(string? value) => UnityEngine.Debug.Log($"{DebugEx.ServerPrefix} [DAP] {value}");
        public override void Info(string? value) => UnityEngine.Debug.Log($"{DebugEx.ServerPrefix} [DAP] {value}");
        public override void Warn(string? value) => UnityEngine.Debug.LogWarning($"{DebugEx.ServerPrefix} [DAP] {value}");
        public override void Error(string? value) => UnityEngine.Debug.LogError($"{DebugEx.ServerPrefix} [DAP] {value}");
        public override void WriteLine(string? value) => UnityEngine.Debug.Log($"{DebugEx.ServerPrefix} [DAP] {value}");

        public override void Dispose() { }
    }

    public void ScheduleRequest<TArgs, TResponse>(Func<TArgs, TResponse> work, IRequestResponder<TArgs> requestResponder)
        where TArgs : class, new()
        where TResponse : Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.ResponseBody
    {
        PendingRequests.Enqueue(new(() => work(requestResponder.Arguments), requestResponder));
    }

    void Update()
    {
        while (PendingRequests.TryDequeue(out var entry))
        {
            try
            {
                entry.RequestResponder.SetResponse(entry.Task());
            }
            catch (ProtocolException ex)
            {
                entry.RequestResponder.SetError(ex);
            }
            catch (Exception ex)
            {
                entry.RequestResponder.SetError(new ProtocolException($"Unhandled {ex.GetType().Name}", ex));
                Debug.LogException(ex);
            }
        }

        bool didDisposeSome = false;
        for (int i = 0; i < Hosts.Count; i++)
        {
            (DebugHost host, TcpListenerManager listener) = Hosts[i];

            if (!host.Protocol.IsRunning || listener.ConnectedClient is null)
            {
                Debug.Log($"{DebugEx.ServerPrefix} [DAP] Disposing dead session");
                host.Dispose();
                listener.Dispose();
                Hosts.RemoveAt(i--);
                didDisposeSome = true;
                continue;
            }

            host.Update();
        }

        if (Listener?.ConnectedClient is not null && Listener.ConnectedClient.Connected)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Starting new session");
            NetworkStream stream = Listener.ConnectedClient.GetStream();
            DebugHost host = new(this, stream, stream, new UnityLogger());
            Hosts.Add((host, Listener));
            host.Run();

            Listener = null;
        }
        else if (Listener is not null && !Listener.IsListening)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Destroying TCP listener");
            Listener?.Dispose();
            Listener = null;
        }
        else if (didDisposeSome)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Destroying TCP listener (and will trying to reopen it on a better port)");
            Listener?.Dispose();
            Listener = null;
        }

        if (Listener is null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Creating new TCP listener");
            for (int i = 8053; i <= 65535; i++)
            {
                try
                {
                    Listener = TcpListenerManager.Listen(IPAddress.Parse("127.0.0.1"), i, "DAP");
                    goto ok;
                }
                catch (SocketException)
                {

                }
            }
            throw new UnreachableException();
        ok:;
        }
    }

    void OnDisable() => Dispose();
    void OnDestroy() => Dispose();

    void Dispose()
    {
        foreach ((_, IRequestResponder requestResponder) in PendingRequests)
        {
            requestResponder.SetError(new ProtocolException("Request canceled"));
        }
        PendingRequests.Clear();

        Debug.Log($"{DebugEx.ServerPrefix} [DAP] Disposing sessions");
        foreach ((DebugHost host, TcpListenerManager listener) in Hosts)
        {
            host.Dispose();
            listener.Dispose();
        }
        Hosts.Clear();

        Listener?.Dispose();
        Listener = null;
    }
}
