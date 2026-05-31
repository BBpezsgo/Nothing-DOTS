using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using OmniSharp.Extensions.LanguageServer.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;

class ExtensionHost : MonoBehaviour
{
    TcpListenerManager? Listener;
    LanguageServer? Server;
    CancellationTokenSource? CancellationTokenSource;
    EntityQuery ProcessorQ;

    void OnEnable()
    {
        CancellationTokenSource = new CancellationTokenSource();
    }

    class UnityLogger : Microsoft.Extensions.Logging.ILogger
    {
        readonly string categoryName;

        public UnityLogger(string categoryName)
        {
            this.categoryName = categoryName;
        }

        class DummyScope : IDisposable
        {
            public void Dispose()
            {

            }
        }

        public IDisposable BeginScope<TState>(TState state) => new DummyScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    Debug.Log($"{DebugEx.ServerPrefix} [LSP/{categoryName}] {eventId} {formatter(state, exception)}");
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning($"{DebugEx.ServerPrefix} [LSP/{categoryName}] {eventId} {formatter(state, exception)}");
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Debug.LogError($"{DebugEx.ServerPrefix} [LSP/{categoryName}] {eventId} {formatter(state, exception)}");
                    break;
                case LogLevel.None:
                default:
                    break;
            }
        }
    }

    class UnityLoggerProvider : ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new UnityLogger(categoryName);

        public void Dispose()
        {

        }
    }

    void Update()
    {
        if (Listener is null || (Listener.ConnectedClient is null && !Listener.IsListening))
        {
            Debug.Log($"{DebugEx.ServerPrefix} [LSP] Disposing old TCP listener, creating new one");
            Listener?.Dispose();
            Listener = TcpListenerManager.Listen(IPAddress.Parse("127.0.0.1"), 8052, "LSP");
        }

        if (Listener.ConnectedClient is not null && Server is null)
        {
            if (ProcessorQ == default)
            {
                ProcessorQ = ConnectionManager.ServerOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(Processor));
            }

            Debug.Log($"{DebugEx.ServerPrefix} [LSP] Creating server");
            NetworkStream stream = Listener.ConnectedClient.GetStream();
            Server = LanguageServer.Create(options =>
            {
                options.WithInput(stream);
                options.WithOutput(stream);

                options.ConfigureLogging(x => x
                    .SetMinimumLevel(LogLevel.Information)
#if UNITY_EDITOR
                    .Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>()
#else
                    .AddLanguageProtocolLogging()
#endif
                );

                options.WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)));

                options.OnRequest("units", () => MainThreadManager.Instance.ScheduleAsync(() =>
                {
                    using NativeArray<Entity> entities = ProcessorQ.ToEntityArray(Allocator.Temp);
                    List<object> res = new(entities.Length);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity entity = entities[i];
                        Processor processor = ConnectionManager.ServerOrDefaultWorld.EntityManager.GetComponentData<Processor>(entity);

                        string? source = null;
                        if (processor.SourceFile != default
                            && processor.SourceFile.Source.IsServer
                            && (ConnectionManager.ServerOrDefaultWorld.IsServer() || ConnectionManager.ServerOrDefaultWorld.IsLocal()))
                        {
                            source = FileChunkManagerSystem.ResolveFile(processor.SourceFile.Name.ToString());
                        }

                        res.Add(new
                        {
                            id = $"{entity.Index}:{entity.Version}",
                            signal = processor.StatusLED.Status.ToString(),
                            source = source,
                        });
                    }
                    return res;
                }));

                options.OnStarted((server, cancellationToken) =>
                {
                    Debug.Log($"{DebugEx.ServerPrefix} [LSP] Server started");
                    return Task.CompletedTask;
                });
            });
            Debug.Log($"{DebugEx.ServerPrefix} [LSP] Initializing server");
            _ = Server.Initialize(CancellationTokenSource?.Token ?? CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.IsCanceled)
                    {
                        Debug.LogWarning($"{DebugEx.ServerPrefix} [LSP] Server initialization canceled");
                    }
                    else if (task.IsFaulted)
                    {
                        Debug.LogError($"{DebugEx.ServerPrefix} [LSP] Server initialization failed");
                        Debug.LogException(task.Exception);
                    }
                    else
                    {
                        Debug.Log($"{DebugEx.ServerPrefix} [LSP] Server initialized");
                    }
                }, TaskScheduler.Default);

            Server.Exit.Subscribe((v) =>
            {
                Debug.Log($"{DebugEx.ServerPrefix} [LSP] Server exited {v}");
                Server?.Dispose();
                Server = null;
                Listener?.Dispose();
            });
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
            Debug.Log($"{DebugEx.ServerPrefix} [LSP] Disposing server");
            Server.Dispose();
            Server = null;
        }

        if (Listener is not null)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [LSP] Disposing TCP listener");
            Listener.Dispose();
            Listener = null;
        }

        try
        {
            if (ProcessorQ != default) ProcessorQ.Dispose();
        }
        catch (NullReferenceException) { }
        ProcessorQ = default;
    }
}
