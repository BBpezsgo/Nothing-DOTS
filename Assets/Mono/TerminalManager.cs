using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using LanguageCore.Runtime;
using NaughtyAttributes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

public class TerminalManager : Singleton<TerminalManager>, IUISetup<Entity>, IUICleanup
{
    Entity selectedUnitEntity = Entity.Null;
    float refreshAt = default;
    ImmutableArray<string> selectingFile = ImmutableArray<string>.Empty;
    int selectingFileI = 0;

    [Header("UI")]
    [SerializeField, ReadOnly] Button? ui_ButtonSelect;
    [SerializeField, ReadOnly] Button? ui_ButtonCompile;
    [SerializeField, ReadOnly] Button? ui_ButtonHalt;
    [SerializeField, ReadOnly] Button? ui_ButtonReset;
    [SerializeField, ReadOnly] Button? ui_ButtonContinue;
    [SerializeField, ReadOnly] Label? ui_labelTerminal;
    [SerializeField, ReadOnly] ScrollView? ui_scrollTerminal;
    [SerializeField, ReadOnly] TextField? ui_inputSourcePath;
    [SerializeField, ReadOnly] UIDocument? ui = default;

    readonly StringBuilder _terminalBuilder = new();
    TerminalEmulator? _terminal;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        if (Time.time >= refreshAt || !selectingFile.IsDefault)
        {
            if (!ConnectionManager.ClientOrDefaultWorld.EntityManager.Exists(selectedUnitEntity))
            {
                UIManager.Instance.CloseUI(this);
                return;
            }

            RefreshUI(selectedUnitEntity);
            refreshAt = Time.time + .2f;
            return;
        }
    }

    public void Setup(UIDocument ui, Entity unitEntity)
    {
        gameObject.SetActive(true);
        this.ui = ui;

        selectedUnitEntity = unitEntity;
        refreshAt = Time.time + .2f;
        selectingFile = ImmutableArray<string>.Empty;
        selectingFileI = 0;

        ui_inputSourcePath = ui.rootVisualElement.Q<TextField>("input-source-path");
        ui_ButtonSelect = ui.rootVisualElement.Q<Button>("button-select");
        ui_ButtonCompile = ui.rootVisualElement.Q<Button>("button-compile");
        ui_ButtonHalt = ui.rootVisualElement.Q<Button>("button-halt");
        ui_ButtonReset = ui.rootVisualElement.Q<Button>("button-reset");
        ui_ButtonContinue = ui.rootVisualElement.Q<Button>("button-continue");
        ui_labelTerminal = ui.rootVisualElement.Q<Label>("label-terminal");
        ui_scrollTerminal = ui.rootVisualElement.Q<ScrollView>("scroll-terminal");

        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            ui_inputSourcePath!.value = processor.SourceFile.Name.ToString();
        }

        BlurTerminal();

        if (!string.IsNullOrWhiteSpace(FileChunkManagerSystem.BasePath))
        {
            ui_ButtonSelect.SetEnabled(true);
            ui_ButtonSelect.clickable = new Clickable(() =>
            {
                selectingFile = Directory.GetFiles(FileChunkManagerSystem.BasePath)
                    .Select(v => Path.GetRelativePath(FileChunkManagerSystem.BasePath, v))
                    .Where(v => !v.EndsWith(".meta"))
                    .ToImmutableArray();
                selectingFileI = 0;
                SelectTerminal();
            });
        }
        else
        {
            ui_ButtonSelect.SetEnabled(false);
        }

        ui_ButtonCompile.clickable = new Clickable(() =>
        {
            World world = ConnectionManager.ClientOrDefaultWorld;

            if (world.IsServer())
            {
                Debug.LogError($"Not implemented");
                return;
            }

            string file =
                string.IsNullOrWhiteSpace(FileChunkManagerSystem.BasePath)
                ? ui_inputSourcePath.value
                : Path.Combine(FileChunkManagerSystem.BasePath, ui_inputSourcePath.value);

            Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(SetProcessorSourceRequestRpc));
            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
            world.EntityManager.SetComponentData(entity, new SetProcessorSourceRequestRpc()
            {
                Source = ui_inputSourcePath.value,
                Version = File.GetLastWriteTimeUtc(file).Ticks,
                Entity = ghostInstance,
            });
        });

        ui_ButtonHalt.clickable = new Clickable(() =>
        {
            World world = ConnectionManager.ClientOrDefaultWorld;

            if (world.IsServer())
            {
                Debug.LogError($"Not implemented");
                return;
            }

            Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(ProcessorCommandRequestRpc));
            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
            world.EntityManager.SetComponentData(entity, new ProcessorCommandRequestRpc()
            {
                Entity = ghostInstance,
                Command = ProcessorCommand.Halt,
                Data = default,
            });
        });

        ui_ButtonReset.clickable = new Clickable(() =>
        {
            World world = ConnectionManager.ClientOrDefaultWorld;

            if (world.IsServer())
            {
                Debug.LogError($"Not implemented");
                return;
            }

            Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(ProcessorCommandRequestRpc));
            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
            world.EntityManager.SetComponentData(entity, new ProcessorCommandRequestRpc()
            {
                Entity = ghostInstance,
                Command = ProcessorCommand.Reset,
                Data = default,
            });
        });

        ui_ButtonContinue.clickable = new Clickable(() =>
        {
            World world = ConnectionManager.ClientOrDefaultWorld;

            if (world.IsServer())
            {
                Debug.LogError($"Not implemented");
                return;
            }

            Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(ProcessorCommandRequestRpc));
            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
            world.EntityManager.SetComponentData(entity, new ProcessorCommandRequestRpc()
            {
                Entity = ghostInstance,
                Command = ProcessorCommand.Continue,
                Data = default,
            });
        });

        RefreshUI(unitEntity);
    }

    void SelectTerminal()
    {
        ui_inputSourcePath!.tabIndex = -1;
        ui_ButtonCompile!.tabIndex = -1;
        ui_ButtonHalt!.tabIndex = -1;
        ui_ButtonReset!.tabIndex = -1;
        ui_ButtonSelect!.tabIndex = -1;
        ui_ButtonContinue!.tabIndex = -1;
        ui_labelTerminal!.tabIndex = -1;
        ui_scrollTerminal!.tabIndex = -1;

        ui_ButtonCompile!.SetEnabled(false);
        ui_ButtonHalt!.SetEnabled(false);
        ui_ButtonReset!.SetEnabled(false);
        ui_ButtonSelect!.SetEnabled(false);
        ui_ButtonContinue!.SetEnabled(false);
        ui_scrollTerminal!.SetEnabled(false);

        ui_inputSourcePath!.focusable = false;
        ui_ButtonCompile!.focusable = false;
        ui_ButtonHalt!.focusable = false;
        ui_ButtonReset!.focusable = false;
        ui_ButtonSelect!.focusable = false;
        ui_ButtonContinue!.focusable = false;
        ui_labelTerminal!.focusable = false;
        ui_scrollTerminal!.focusable = false;

        ui_labelTerminal!.Focus();
    }

    void BlurTerminal()
    {
        ui_inputSourcePath!.tabIndex = 0;
        ui_ButtonCompile!.tabIndex = 0;
        ui_ButtonSelect!.tabIndex = 0;
        ui_ButtonHalt!.tabIndex = 0;
        ui_ButtonReset!.tabIndex = 0;
        ui_ButtonContinue!.tabIndex = 0;
        ui_labelTerminal!.tabIndex = 0;
        ui_scrollTerminal!.tabIndex = 0;

        ui_ButtonCompile!.SetEnabled(true);
        ui_ButtonSelect!.SetEnabled(true);
        ui_ButtonHalt!.SetEnabled(true);
        ui_ButtonReset!.SetEnabled(true);
        ui_ButtonContinue!.SetEnabled(true);
        ui_scrollTerminal!.SetEnabled(true);

        ui_inputSourcePath!.focusable = true;
        ui_ButtonCompile!.focusable = true;
        ui_ButtonSelect!.focusable = true;
        ui_ButtonHalt!.focusable = true;
        ui_ButtonReset!.focusable = true;
        ui_ButtonContinue!.focusable = true;
        ui_labelTerminal!.focusable = true;
        ui_scrollTerminal!.focusable = true;
    }

    public unsafe void RefreshUI(Entity unitEntity)
    {
        bool isBottom = ui_scrollTerminal!.scrollOffset == ui_labelTerminal!.layout.max - ui_scrollTerminal.contentViewport.layout.size;
        _terminalBuilder.Clear();

        if (!selectingFile.IsEmpty)
        {
            _terminal = null;
            if (Input.GetKeyDown(KeyCode.Q))
            {
                selectingFile = ImmutableArray<string>.Empty;
                BlurTerminal();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) ||
                    Input.GetKeyDown(KeyCode.S))
                {
                    selectingFileI++;
                    if (selectingFileI >= selectingFile.Length) selectingFileI -= selectingFile.Length;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow) ||
                    Input.GetKeyDown(KeyCode.W))
                {
                    selectingFileI--;
                    if (selectingFileI < 0) selectingFileI += selectingFile.Length;
                }

                if (Input.GetKeyDown(KeyCode.Return))
                {
                    ui_inputSourcePath!.value = selectingFile[selectingFileI];
                    selectingFileI = 0;
                    selectingFile = ImmutableArray<string>.Empty;
                    BlurTerminal();
                    return;
                }

                for (int i = 0; i < selectingFile.Length; i++)
                {
                    if (selectingFileI == i) _terminalBuilder.Append("> ");
                    else _terminalBuilder.Append("  ");
                    _terminalBuilder.Append(selectingFile[i]);
                    _terminalBuilder.AppendLine();
                }
                ui_labelTerminal!.text = _terminalBuilder.ToString();
            }
        }
        else
        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            if (processor.SourceFile == default)
            {
                _terminal = null;
                _terminalBuilder.AppendLine("<color=red>No source</color>");
            }
            else
            {
                CompiledSource source = CompilerManager.Instance.CompiledSources[processor.SourceFile];
                const string SpinnerChars = "-\\|/";
                char spinner = SpinnerChars[(int)(MonoTime.Now * 8f) % SpinnerChars.Length];

                if (source.Status != CompilationStatus.Done || !source.IsSuccess)
                {
                    switch (source.Status)
                    {
                        case CompilationStatus.None:
                            break;
                        case CompilationStatus.Secuedued:
                            if (source.Status == CompilationStatus.Secuedued &&
                                !float.IsNaN(source.Progress))
                            {
                                _terminalBuilder.Append("Compiling ");
                                _terminalBuilder.Append(spinner);
                                _terminalBuilder.AppendLine();
                            }
                            else
                            {
                                _terminalBuilder.Append("Secuedued ");
                                _terminalBuilder.Append(spinner);
                                _terminalBuilder.AppendLine();
                            }
                            break;
                        case CompilationStatus.Compiling:
                            _terminalBuilder.Append("Compiling ");
                            _terminalBuilder.Append(spinner);
                            _terminalBuilder.AppendLine();
                            break;
                        case CompilationStatus.Compiled:
                            break;
                        case CompilationStatus.Done:
                            break;
                    }
                }

                if (!float.IsNaN(source.Progress) && source.Progress != 1f)
                {
                    const int progressBarWidth = 10;

                    int progressBarFilledWidth = math.clamp((int)(source.Progress * progressBarWidth), 0, progressBarWidth);
                    int progressBarEmptyWidth = progressBarWidth - progressBarFilledWidth;

                    _terminalBuilder.Append("Uploading [");
                    _terminalBuilder.Append('#', progressBarFilledWidth);
                    _terminalBuilder.Append(' ', progressBarEmptyWidth);
                    _terminalBuilder.Append(']');
                    _terminalBuilder.AppendLine();
                }

                switch (source.Status)
                {
                    case CompilationStatus.Secuedued:
                        {
                            _terminal = null;
                            if (float.IsNaN(source.Progress))
                            {
                                if (source.CompileSecuedued == default)
                                {
                                    _terminalBuilder.AppendLine("Compilation in ? sec ");
                                }
                                else
                                {
                                    _terminalBuilder.AppendLine($"Compilation in {math.max(0f, source.CompileSecuedued - MonoTime.Now):#.0} sec ");
                                }
                                _terminalBuilder.Append(spinner);
                                _terminalBuilder.AppendLine();
                            }
                            break;
                        }
                    case CompilationStatus.Compiling:
                        {
                            _terminal = null;
                            break;
                        }
                    case CompilationStatus.Compiled:
                        {
                            _terminal = null;
                            _terminalBuilder.AppendLine("Compiled");
                            break;
                        }
                    case CompilationStatus.Done:
                        {
                            if (source.IsSuccess)
                            {
                                _terminal ??= new TerminalEmulator(ui_labelTerminal);
                                _terminal.Update();
                                switch (processor.Signal)
                                {
                                    case Signal.None:
                                        _terminalBuilder.Append(processor.StdOutBuffer.ToString());
                                        if (processor.IsKeyRequested)
                                        {
                                            char c = _terminal.RequestKey();
                                            if (c != default)
                                            {
                                                World world = ConnectionManager.ClientOrDefaultWorld;

                                                if (world.IsServer())
                                                {
                                                    Debug.LogError($"Not implemented");
                                                }
                                                else
                                                {
                                                    Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(ProcessorCommandRequestRpc));
                                                    GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
                                                    world.EntityManager.SetComponentData(entity, new ProcessorCommandRequestRpc()
                                                    {
                                                        Entity = ghostInstance,
                                                        Command = ProcessorCommand.Key,
                                                        Data = unchecked((ushort)c),
                                                    });
                                                }
                                            }
                                            else if (Time.time % 1f < .5f)
                                            {
                                                _terminalBuilder.Append("<mark=#ffffffff>_</mark>");
                                            }
                                        }
                                        break;
                                    case Signal.UserCrash:
                                        // RuntimeException exception = new(
                                        //     HeapUtils.GetString(new ReadOnlySpan<byte>(&processor.Memory, Processor.TotalMemorySize), processor.Crash) ?? "null",
                                        //     new RuntimeContext(
                                        //         processor.Registers,
                                        //         new ReadOnlySpan<byte>(&processor.Memory, 1024).ToImmutableArray(),
                                        //         source.Code!.Value.ToImmutableArray(),
                                        //         ProcessorSystemServer.BytecodeInterpreterSettings.StackStart
                                        //     ),
                                        //     source.DebugInformation);
                                        // terminalBuilder.AppendLine(exception.ToString(false));
                                        _terminalBuilder.AppendLine("<color=red>Crashed</color>");
                                        break;
                                    case Signal.StackOverflow:
                                        _terminalBuilder.AppendLine("<color=red>Stack overflow</color>");
                                        break;
                                    case Signal.Halt:
                                        _terminalBuilder.AppendLine("Halted");
                                        break;
                                    case Signal.UndefinedExternalFunction:
                                        _terminalBuilder.AppendLine($"<color=red>Undefined external function {processor.Crash}</color>");
                                        break;
                                }
                            }
                            else
                            {
                                _terminal = null;
                                _terminalBuilder.AppendLine("<color=red>Compile failed</color>");
                                foreach (LanguageCore.Diagnostic item in source.Diagnostics.Diagnostics)
                                {
                                    if (item.Level is
                                        LanguageCore.DiagnosticsLevel.Hint or
                                        LanguageCore.DiagnosticsLevel.OptimizationNotice or
                                        LanguageCore.DiagnosticsLevel.Information)
                                    { continue; }

                                    _terminalBuilder.AppendLine(item.ToString());
                                    (string SourceCode, string Arrows)? arrows = item.GetArrows((uri) =>
                                    {
                                        if (!uri.TryGetNetcode(out FileId fileId)) return null;

                                        if (FileChunkManagerSystem.GetInstance(ConnectionManager.ClientOrDefaultWorld).TryGetRemoteFile(fileId, out RemoteFile remoteFile))
                                        {
                                            return Encoding.UTF8.GetString(remoteFile.File.Data);
                                        }

                                        return null;
                                    });
                                    if (arrows.HasValue)
                                    {
                                        _terminalBuilder.AppendLine(arrows.Value.SourceCode);
                                        _terminalBuilder.AppendLine(arrows.Value.Arrows);
                                    }
                                }
                                foreach (LanguageCore.DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext)
                                {
                                    _terminalBuilder.AppendLine(item.ToString());
                                }
                            }
                            break;
                        }
                    case CompilationStatus.None:
                    default:
                        {
                            _terminal = null;
                            _terminalBuilder.AppendLine("???");
                            break;
                        }
                }
            }
            ui_labelTerminal.text = _terminalBuilder.ToString();
        }

        if (isBottom)
        {
            ui_scrollTerminal!.scrollOffset = ui_labelTerminal!.layout.max - ui_scrollTerminal.contentViewport.layout.size;
        }
    }

    public void Cleanup(UIDocument ui)
    {
        selectedUnitEntity = Entity.Null;
        refreshAt = float.PositiveInfinity;
        selectingFile = ImmutableArray<string>.Empty;
        selectingFileI = 0;
        _terminal = null;

        if (ui != null &&
            ui.rootVisualElement != null)
        {
            ui_ButtonSelect!.clickable = null;
            ui_ButtonCompile!.clickable = null;
            ui_ButtonHalt!.clickable = null;
            ui_ButtonReset!.clickable = null;
            ui_ButtonContinue!.clickable = null;
            ui_labelTerminal!.text = string.Empty;
            BlurTerminal();
        }
        gameObject.SetActive(false);
    }
}
