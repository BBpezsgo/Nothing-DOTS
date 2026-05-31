using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DebugServer;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Runtime;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Unity.Collections;
using Unity.Entities;

partial class DebugHost : BytecodeDebugAdapterBase, IDisposable
{
    ProcessorSource _originalSource;
    FileId _originalFile;
    Entity _entity;
    readonly DebugHostManager _manager;

    public DebugHost(DebugHostManager manager, Stream stdIn, Stream stdOut, Logger log) : base(stdIn, stdOut, log)
    {
        _manager = manager;
    }

    unsafe bool GetProcessor([NotNullWhen(true)] out ReadOnlyProcessorState processor)
    {
        processor = default;

        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (!e.Exists(_entity)) return false;

        Processor _processor = e.GetComponentData<Processor>(_entity);
        if (_processor.Source != _originalSource) return false;

        processor = new ReadOnlyProcessorState(
            ProcessorSystemServer.BytecodeInterpreterSettings,
            _processor.Registers,
            new Span<byte>(Unsafe.AsPointer(ref _processor.Memory), global::Processor.TotalMemorySize),
            _processor.Source.Code.AsSpan(),
            _processor.Source.GeneratedFunctions.AsSpan(),
            _processor.Crash,
            _processor.Signal
        );
        return true;
    }

    bool GetSource(FileId file, [NotNullWhen(true)] out CompiledSourceServer? source)
    {
        CompilerSystemServer compilerSystem = ConnectionManager.ServerOrDefaultWorld.GetExistingSystemManaged<CompilerSystemServer>();
        return compilerSystem.CompiledSources.TryGetValue(file, out source);
    }

    protected override CompilerResult Compiled => GetSource(_originalFile, out CompiledSourceServer? source) ? source.Compiled : CompilerResult.MakeEmpty(_originalFile.ToUri());
    protected override ReadOnlyProcessorState Processor => GetProcessor(out ReadOnlyProcessorState processor) ? processor : default;
    protected override CompiledDebugInformation DebugInformation => GetSource(_originalFile, out CompiledSourceServer? source) ? source.DebugInformation : new(null);

    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        if (!Uri.TryCreate(arguments.Source.Path, UriKind.Absolute, out Uri? fileUri) || !FileId.FromUri(fileUri, out _))
        {
            return new SourceResponse();
        }

        NetcodeSourceProviderOffline sourceProvider = new();
        SourceProviderResultSync res = sourceProvider.TryLoad(arguments.Source.Path, null);

        if (res.Type != SourceProviderResultType.Success || res.Stream is null)
        {
            return new SourceResponse();
        }

        StreamReader reader = new(res.Stream);
        string content = reader.ReadToEnd();
        res.Stream.Dispose();
        reader.Dispose();

        return new SourceResponse(content);
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        Log.Trace("[Handler] Disconnect");

        IsDisconnected = true;

        Continue(null);

        return new DisconnectResponse();
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        return new SetExceptionBreakpointsResponse()
        {
            Breakpoints = new(),
        };
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
    {
        InitializeResponse res = base.HandleInitializeRequest(arguments);
        res.SupportsConfigurationDoneRequest = false;
        return res;
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        Log.Trace($"[Handler] Launch");

        Entity entity;

        {
            string entityId = arguments.ConfigurationProperties.GetValueAsString("entity");
            if (string.IsNullOrEmpty(entityId))
            {
                throw new ProtocolException("Launch failed because launch configuration did not specify 'entity'.");
            }

            string[] parts = entityId.Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out int entityIndex)
                || !int.TryParse(parts[1], out int entityVersion))
            {
                throw new ProtocolException($"Launch failed because the entity is invalid.");
            }

            entity = new Entity()
            {
                Index = entityIndex,
                Version = entityVersion,
            };
        }

        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;

        if (!e.Exists(entity))
        {
            Log.Error($"Entity {entity} doesn't exists");
            throw new ProtocolException($"Launch failed because entity {entity} doesn't exist.");
        }

        if (!e.HasComponent<Processor>(entity))
        {
            Log.Error($"Entity {entity} doesn't have a {typeof(Processor)} component");
            throw new ProtocolException($"Launch failed because entity {entity} doesn't have a {typeof(Processor)} component.");
        }

        Log.Trace($"Disposing previous session");
        DisposeSession();

        NoDebug = arguments.NoDebug ?? false;

        Processor _processor = e.GetComponentData<Processor>(entity);

        _entity = entity;
        _originalFile = _processor.SourceFile;
        _originalSource = _processor.Source;
        _processor.DebugContext = new ProcessorJob.DebugContext()
        {
            IsBeingDebugged = true,
            Breakpoints = new FixedList128Bytes<ushort>(),
            Stopped = ProcessorJob.StopReason.No,
            IsStopUnhandled = false,
        };

        e.SetComponentData(_entity, _processor);

        return new LaunchResponse();
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        SetBreakpointsResponse res = base.HandleSetBreakpointsRequest(arguments);
        RefreshProcessorBreakpoints();
        return res;
    }

    protected override SetInstructionBreakpointsResponse HandleSetInstructionBreakpointsRequest(SetInstructionBreakpointsArguments arguments)
    {
        SetInstructionBreakpointsResponse res = base.HandleSetInstructionBreakpointsRequest(arguments);
        RefreshProcessorBreakpoints();
        return res;
    }

    void RefreshProcessorBreakpoints()
    {
        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (e.Exists(_entity))
        {
            Processor _processor = e.GetComponentData<Processor>(_entity);
            _processor.DebugContext.Breakpoints.Clear();
            foreach ((Breakpoint Breakpoint, InstructionBreakpoint InstructionBreakpoint, int Address) item in _instructionBreakpoints)
            {
                _processor.DebugContext.Breakpoints.Add((ushort)item.Address);
            }
            foreach (CompiledBreakpoint? item in _breakpoints.Values.SelectMany(v => v))
            {
                _processor.DebugContext.Breakpoints.Add((ushort)item.Instruction);
            }
            e.SetComponentData(_entity, _processor);
        }
    }

    protected override Source ToSource(Uri file)
    {
        if (!FileId.FromUri(file, out FileId fileId))
        {
            return new Source()
            {
                Name = Path.GetFileName(file.ToString()),
                Path = file.ToString(),
            };
        }

        if (fileId.Source.IsServer)
        {
            string? localFile = FileChunkManagerSystem.ResolveFile(fileId.Name.ToString());
            if (localFile is not null)
            {
                return new Source()
                {
                    Name = Path.GetFileName(file.ToString()),
                    Path = localFile,
                    Origin = fileId.Source.ToString(),
                };
            }
        }

        return new Source()
        {
            Name = Path.GetFileName(file.ToString()),
            Path = file.ToString(),
            Origin = fileId.Source.ToString(),
        };
    }

    protected override Uri ToUri(Source source)
    {
        CompilerSystemServer compilerSystem = ConnectionManager.ServerOrDefaultWorld.GetExistingSystemManaged<CompilerSystemServer>();

        foreach (var item in Compiled.RawStatements.Select(v => v.File))
        {
            if (FileId.FromUri(item, out FileId fileId))
            {
                if (fileId.Source.IsServer)
                {
                    string? localFile = FileChunkManagerSystem.ResolveFile(fileId.Name.ToString());
                    if (localFile == source.Path)
                    {
                        return fileId.ToUri();
                    }
                }
            }
        }

        return base.ToUri(source);
    }

    protected override void Continue(StopReason? step)
    {
        if (step is not null)
        {
            RequestStop(step);
        }
        else
        {
            EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
            if (e.Exists(_entity))
            {
                Processor processor = e.GetComponentData<Processor>(_entity);
                processor.DebugContext.Stopped = ProcessorJob.StopReason.No;
                processor.DebugContext.IsStopUnhandled = false;
                e.SetComponentData(_entity, processor);
            }
        }
    }

    protected override void RequestStop(StopReason reason)
    {
        if (NoDebug) throw new InvalidOperationException($"Cannot stop the runtime in no-debug mode");

        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (e.Exists(_entity))
        {
            Processor processor = e.GetComponentData<Processor>(_entity);
            processor.DebugContext.Stopped = reason switch
            {
                StopReason_Crash => ProcessorJob.StopReason.Signal,
                StopReason_Breakpoint => ProcessorJob.StopReason.Breakpoint,
                StopReason_StepForward => ProcessorJob.StopReason.Pause,
                StopReason_StepIn => ProcessorJob.StopReason.Pause,
                StopReason_StepOut => ProcessorJob.StopReason.Pause,
                StopReason_StepInstruction => ProcessorJob.StopReason.Pause,
                StopReason_Pause => ProcessorJob.StopReason.Pause,
                _ => throw new NotImplementedException(),
            };
            processor.DebugContext.IsStopUnhandled = true;
            e.SetComponentData(_entity, processor);
        }
    }

    protected override void SendKey(byte c)
    {
        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (!e.Exists(_entity)) return;

        Processor _processor = e.GetComponentData<Processor>(_entity);
        if (_processor.Source != _originalSource) return;

        if (_processor.InputKey.Length >= _processor.InputKey.Capacity)
        {
            Debug.LogWarning($"{DebugEx.ServerPrefix} Standard input buffer is full");
            return;
        }

        _processor.InputKey.Add((char)c);
        e.SetComponentData(_entity, _processor);
    }

    public override void Run()
    {
        Log.Info("Starting protocol");
        Protocol.Run();
    }

    public void Update()
    {
        if (Protocol.IsRunning && IsDisconnected)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Stopping protocol");
            Protocol.Stop();
        }

        if (_entity == Entity.Null)
        {
            return;
        }

        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (!e.Exists(_entity))
        {
            Protocol.SendEvent(new ExitedEvent() { ExitCode = 1 });
            Protocol.SendEvent(new TerminatedEvent());
            _entity = Entity.Null;
            return;
        }

        Processor processor = e.GetComponentData<Processor>(_entity);
        if (processor.DebugContext.Stopped != ProcessorJob.StopReason.No && processor.DebugContext.IsStopUnhandled)
        {
            processor.DebugContext.IsStopUnhandled = false;

            GatherInformation();
            switch (processor.DebugContext.Stopped)
            {
                case ProcessorJob.StopReason.Pause:
                    Protocol.SendEvent(new StoppedEvent()
                    {
                        Reason = StoppedEvent.ReasonValue.Pause,
                        AllThreadsStopped = true,
                        ThreadId = 1,
                    });
                    break;
                case ProcessorJob.StopReason.Breakpoint:
                    bool shouldContinue = true;
                    List<int> hitBreakpoints = new();

                    foreach (var item in _instructionBreakpoints)
                    {
                        if (item.Address != processor.Registers.CodePointer
                            || !item.Breakpoint.Id.HasValue)
                        { continue; }

                        hitBreakpoints.Add(item.Breakpoint.Id.Value);
                        shouldContinue = false;
                    }

                    foreach (CompiledBreakpoint breakpoint in _breakpoints.Values.SelectMany(v => v))
                    {
                        if (breakpoint.Instruction != processor.Registers.CodePointer
                            || !breakpoint.Breakpoint.Id.HasValue)
                        { continue; }

                        if (!string.IsNullOrWhiteSpace(breakpoint.Condition))
                        {
                            DiagnosticsCollection diagnostics = new();

                            if (TryEvaluate(breakpoint.Condition, StackFrames.Count > 0 ? StackFrames[0].Id : null, diagnostics, out bool result))
                            {
                                if (!result) goto skip;
                            }
                            else
                            {
                                StringBuilder b = new();
                                b.AppendLine($"Failed to evaluate breakpoint condition `{breakpoint.Condition}` at {breakpoint.SourceBreakpoint.Line}:{breakpoint.SourceBreakpoint.Column} in {breakpoint.Breakpoint.Source.Name}");
                                diagnostics.WriteErrorsTo(b);
                                Protocol.SendEvent(new OutputEvent()
                                {
                                    Output = b.ToString(),
                                    Severity = OutputEvent.SeverityValue.Error,
                                });
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(breakpoint.LogMessage))
                        {
                            List<ExpressionVariable> variables = StackFrames.Count > 0 ? GetExpressionVariables(StackFrames[0].Id) : new();
                            string template = breakpoint.LogMessage;
                            int i = 0;
                            StringBuilder res = new();
                            while (i < template.Length)
                            {
                                int j = template.IndexOf('{', i);
                                if (j != -1)
                                {
                                    int k = template.IndexOf('}', j);
                                    if (k != -1)
                                    {
                                        res.Append(template[i..j]);

                                        string item = template[(j + 1)..k];

                                        if (variables.TryFind(v => v.Name == item, out ExpressionVariable variable))
                                        {
                                            UniqueIds uniqueIds = new();
                                            unsafe
                                            {
                                                res.Append(ToVariable(variable.Address, variable.Type, new ReadOnlySpan<byte>(Unsafe.AsPointer(ref processor.Memory.Memory), global::Processor.TotalMemorySize), variable.Name, ref uniqueIds).Value);
                                            }
                                        }

                                        i = k + 1;
                                        continue;
                                    }
                                }

                                res.Append(template[i..]);
                                break;
                            }
                            res.AppendLine();
                            Protocol.SendEvent(new OutputEvent()
                            {
                                Output = res.ToString(),
                                Category = OutputEvent.CategoryValue.Console,
                                Source = breakpoint.Breakpoint.Source,
                                Line = breakpoint.SourceBreakpoint.Line,
                                Column = breakpoint.SourceBreakpoint.Column,
                            });
                            goto skip;
                        }

                        hitBreakpoints.Add(breakpoint.Breakpoint.Id.Value);
                        shouldContinue = false;
                    skip:;
                    }

                    if (shouldContinue)
                    {
                        processor.DebugContext.Stopped = ProcessorJob.StopReason.No;
                        break;
                    }

                    Protocol.SendEvent(new StoppedEvent()
                    {
                        Reason = StoppedEvent.ReasonValue.Breakpoint,
                        AllThreadsStopped = true,
                        ThreadId = 1,
                        HitBreakpointIds = hitBreakpoints,
                    });
                    break;
                case ProcessorJob.StopReason.Signal:
                    if (processor.Signal == Signal.Halt)
                    {
                        Protocol.SendEvent(new StoppedEvent()
                        {
                            Reason = StoppedEvent.ReasonValue.Pause,
                            AllThreadsStopped = true,
                            ThreadId = 1,
                            Description = "Processor halted",
                        });
                    }
                    else
                    {
                        Protocol.SendEvent(new StoppedEvent()
                        {
                            Reason = StoppedEvent.ReasonValue.Exception,
                            AllThreadsStopped = true,
                            ThreadId = 1,
                            Description = processor.Signal switch
                            {
                                Signal.UserCrash => $"Crashed ({processor.Crash})",
                                Signal.StackOverflow => $"Stack Overflow",
                                Signal.UndefinedExternalFunction => $"Undefined external function {processor.Crash}",
                                Signal.PointerOutOfRange => $"Pointer out of Range",
                                Signal.None => "Crashed",
                                Signal.Halt or _ => throw new UnreachableException(),
                            },
                        });
                    }
                    break;
                case ProcessorJob.StopReason.RuntimeException:
                    Protocol.SendEvent(new StoppedEvent()
                    {
                        Reason = StoppedEvent.ReasonValue.Exception,
                        AllThreadsStopped = true,
                        ThreadId = 1,
                        Description = "Unhandled RuntimeException",
                    });
                    break;
                case ProcessorJob.StopReason.No:
                default:
                    throw new UnreachableException();
            }
            e.SetComponentData(_entity, processor);
        }
    }

    protected override void DisposeSession()
    {
        EntityManager e = ConnectionManager.ServerOrDefaultWorld.EntityManager;
        if (e.Exists(_entity))
        {
            Processor _processor = e.GetComponentData<Processor>(_entity);
            _processor.DebugContext = default;
            e.SetComponentData(_entity, _processor);
        }
    }

    public void Dispose()
    {
        if (Protocol.IsRunning)
        {
            Debug.Log($"{DebugEx.ServerPrefix} [DAP] Stopping protocol");
            Protocol.Stop();
        }

        DisposeSession();
    }
}
