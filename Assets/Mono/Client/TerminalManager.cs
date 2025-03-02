using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

    [Header("UI")]
    [SerializeField, NotNull] VisualTreeAsset? FileItem = default;
    [SerializeField, ReadOnly] UIDocument? ui = default;

    Button? ui_ButtonSelect;
    Button? ui_ButtonCompile;
    Button? ui_ButtonHalt;
    Button? ui_ButtonReset;
    Button? ui_ButtonContinue;
    Label? ui_labelTerminal;
    ScrollView? ui_scrollTerminal;
    ScrollView? ui_scrollFiles;
    TextField? ui_inputSourcePath;
    ProgressBar? ui_progressCompilation;

    readonly StringBuilder _terminalBuilder = new();
    TerminalEmulator? _terminal;
    byte[]? _memory;
    ProgressRecord<(int, int)>? _memoryDownloadProgress;
    Awaitable<RemoteFile>? _memoryDownloadTask;
    string? _scheduledSource;

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
        _memory = null;
        _memoryDownloadProgress = null;
        // try { _memoryDownloadTask?.Cancel(); } catch { }
        _memoryDownloadTask = null;
        _scheduledSource = null;

        ui_inputSourcePath = ui.rootVisualElement.Q<TextField>("input-source-path");
        ui_ButtonSelect = ui.rootVisualElement.Q<Button>("button-select");
        ui_ButtonCompile = ui.rootVisualElement.Q<Button>("button-compile");
        ui_ButtonHalt = ui.rootVisualElement.Q<Button>("button-halt");
        ui_ButtonReset = ui.rootVisualElement.Q<Button>("button-reset");
        ui_ButtonContinue = ui.rootVisualElement.Q<Button>("button-continue");
        ui_labelTerminal = ui.rootVisualElement.Q<Label>("label-terminal");
        ui_scrollTerminal = ui.rootVisualElement.Q<ScrollView>("scroll-terminal");
        ui_scrollFiles = ui.rootVisualElement.Q<ScrollView>("scroll-files");
        ui_progressCompilation = ui.rootVisualElement.Q<ProgressBar>("progress-compilation");

        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            ui_inputSourcePath!.value = processor.SourceFile.Name.ToString();
        }

        EndFileSelection();

        if (!string.IsNullOrWhiteSpace(FileChunkManagerSystem.BasePath))
        {
            ui_ButtonSelect.SetEnabled(true);
            ui_ButtonSelect.clickable = new Clickable(() =>
            {
                selectingFile = Directory.GetFiles(FileChunkManagerSystem.BasePath)
                    .Select(v => Path.GetRelativePath(FileChunkManagerSystem.BasePath, v))
                    .Where(v => !v.EndsWith(".meta"))
                    .ToImmutableArray();
                BeginFileSelection();
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
                Entity = ghostInstance,
            });

            _scheduledSource = ui_inputSourcePath.value;
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

    void BeginFileSelection()
    {
        ui_ButtonCompile!.SetEnabled(false);
        ui_ButtonHalt!.SetEnabled(false);
        ui_ButtonReset!.SetEnabled(false);
        ui_ButtonSelect!.SetEnabled(false);
        ui_ButtonContinue!.SetEnabled(false);

        ui_scrollFiles!.style.display = DisplayStyle.Flex;
        ui_scrollTerminal!.style.display = DisplayStyle.None;
    }

    void EndFileSelection()
    {
        ui_ButtonCompile!.SetEnabled(true);
        ui_ButtonSelect!.SetEnabled(true);
        ui_ButtonHalt!.SetEnabled(true);
        ui_ButtonReset!.SetEnabled(true);
        ui_ButtonContinue!.SetEnabled(true);

        ui_scrollFiles!.style.display = DisplayStyle.None;
        ui_scrollTerminal!.style.display = DisplayStyle.Flex;
    }

    static readonly string[] ProgressStatusClasses = new string[]
    {
        "error",
        "warning",
        "success",
    };

    public unsafe void RefreshUI(Entity unitEntity)
    {
        if (!selectingFile.IsEmpty)
        {
            ui_scrollFiles!.SyncList(selectingFile, FileItem, (file, element, recycled) =>
            {
                element.userData = file;
                element.Q<Button>().text = file;
                if (!recycled) element.Q<Button>().clicked += () =>
                {
                    ui_inputSourcePath!.value = (string)element.userData;
                    selectingFile = ImmutableArray<string>.Empty;
                    EndFileSelection();
                };
            });

            _terminal = null;

            if (Input.GetKeyDown(KeyCode.Q))
            {
                selectingFile = ImmutableArray<string>.Empty;
                EndFileSelection();
            }
            return;
        }

        bool isBottom = ui_scrollTerminal!.scrollOffset == ui_labelTerminal!.layout.max - ui_scrollTerminal.contentViewport.layout.size;
        _terminalBuilder.Clear();

        void SetProgressStatus(string? status)
        {
            foreach (string item in ProgressStatusClasses)
            {
                ui_progressCompilation!.EnableInClassList(item, item == status);
            }
        }

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
        if (processor.SourceFile == default || !ConnectionManager.ClientOrDefaultWorld.GetExistingSystemManaged<CompilerSystemClient>().CompiledSources.TryGetValue(processor.SourceFile, out CompiledSource? source))
        {
            _terminal = null;
            if (_scheduledSource != null)
            {
                ui_progressCompilation!.title = "Scheduled ...";
                ui_progressCompilation!.value = 0f;
                SetProgressStatus(null);
            }
            else
            {
                ui_progressCompilation!.title = "No source";
                ui_progressCompilation!.value = 0f;
                SetProgressStatus(null);
            }
        }
        else
        {
            _scheduledSource = null;
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
                            ui_progressCompilation!.title = "Compiling ...";
                            ui_progressCompilation!.value = 0f;
                            SetProgressStatus(null);
                        }
                        else
                        {
                            ui_progressCompilation!.title = "Secuedued ...";
                            ui_progressCompilation!.value = 0f;
                            SetProgressStatus(null);
                        }
                        break;
                    case CompilationStatus.Compiling:
                        ui_progressCompilation!.title = "Compiling ...";
                        ui_progressCompilation!.value = 0f;
                        SetProgressStatus(null);
                        break;
                    case CompilationStatus.Compiled:
                        break;
                    case CompilationStatus.Done:
                        break;
                }
            }

            if (source.Status != CompilationStatus.Done && !float.IsNaN(source.Progress))
            {
                const int progressBarWidth = 10;

                int progressBarFilledWidth = math.clamp((int)(source.Progress * progressBarWidth), 0, progressBarWidth);
                int progressBarEmptyWidth = progressBarWidth - progressBarFilledWidth;

                ui_progressCompilation!.title = "Uploading ...";
                ui_progressCompilation!.value = source.Progress;
                SetProgressStatus(null);
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
                                ui_progressCompilation!.title = "Compilation in ? sec";
                                SetProgressStatus(null);
                            }
                            else
                            {
                                ui_progressCompilation!.title = $"Compilation in {math.max(0f, source.CompileSecuedued - MonoTime.Now):#.0} sec ";
                                SetProgressStatus(null);
                            }
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
                        ui_progressCompilation!.title = "Compiled";
                        ui_progressCompilation!.value = 1f;
                        SetProgressStatus(null);
                        break;
                    }
                case CompilationStatus.Done:
                    {
                        if (source.IsSuccess)
                        {
                            ui_progressCompilation!.title = "Running";
                            ui_progressCompilation!.value = 1f;
                            SetProgressStatus("success");
                            _terminal ??= new TerminalEmulator(ui_labelTerminal);
                            _terminal.Update();
                            switch (processor.Signal)
                            {
                                case Signal.None:
                                    _memory = null;
                                    _memoryDownloadProgress = null;
                                    // try { _memoryDownloadTask?.Cancel(); } catch { }
                                    _memoryDownloadTask = null;
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
                                    ui_progressCompilation!.title = "Crashed";
                                    ui_progressCompilation!.value = 1f;
                                    SetProgressStatus("error");
                                    if (_memory is null)
                                    {
                                        _memoryDownloadProgress ??= new ProgressRecord<(int, int)>(null);

                                        if (_memoryDownloadTask == null)
                                        {
                                            GhostInstance ghostInstance = ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<GhostInstance>(selectedUnitEntity);
                                            // Debug.Log($"Requesting memory for ghost {{ id: {ghostInstance.ghostId} spawnTick: {ghostInstance.spawnTick} ({ghostInstance.spawnTick.SerializedData}) }} ...");
                                            _memoryDownloadTask = FileChunkManagerSystem.GetInstance(ConnectionManager.ClientOrDefaultWorld)
                                                .RequestFile(new FileId($"/i/e/{ghostInstance.ghostId}_{ghostInstance.spawnTick.SerializedData}/m", NetcodeEndPoint.Server), _memoryDownloadProgress);
                                        }

                                        var awaiter = _memoryDownloadTask.GetAwaiter();
                                        if (awaiter.IsCompleted)
                                        {
                                            // Debug.Log("Memory loaded");
                                            RemoteFile result = awaiter.GetResult();
                                            switch (result.Kind)
                                            {
                                                case FileResponseStatus.NotFound:
                                                    ui_progressCompilation!.title = "Crashed (no memory)";
                                                    ui_progressCompilation!.value = 1f;
                                                    break;
                                                default:
                                                    _memory = result.File.Data;
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            ui_progressCompilation!.title = "Crashed (loading memory)";
                                            ui_progressCompilation!.value = (float)_memoryDownloadProgress.Progress.Item1 / (float)_memoryDownloadProgress.Progress.Item2;
                                        }
                                    }
                                    else
                                    {
                                        string? message = HeapUtils.GetString(_memory, processor.Crash);
                                        _terminalBuilder.Append("<color=red>");
                                        _terminalBuilder.Append(message);
                                        _terminalBuilder.Append("</color>");
                                        _terminalBuilder.AppendLine();
                                    }
                                    break;
                                case Signal.StackOverflow:
                                    ui_progressCompilation!.title = "Stack overflow";
                                    ui_progressCompilation!.value = 1f;
                                    SetProgressStatus("error");
                                    break;
                                case Signal.Halt:
                                    ui_progressCompilation!.title = "Halted";
                                    ui_progressCompilation!.value = 1f;
                                    SetProgressStatus("warning");
                                    break;
                                case Signal.UndefinedExternalFunction:
                                    ui_progressCompilation!.title = "Runtime Error";
                                    ui_progressCompilation!.value = 1f;
                                    SetProgressStatus("error");
                                    _terminalBuilder.AppendLine($"<color=red>Undefined external function {processor.Crash}</color>");
                                    break;
                            }
                        }
                        else
                        {
                            ui_progressCompilation!.title = "Compile failed";
                            SetProgressStatus("error");
                            _terminal = null;
                            // _terminalBuilder.AppendLine("<color=red>Compile failed</color>");
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
        _terminal = null;
        _memory = null;
        _memoryDownloadProgress = null;
        // try { _memoryDownloadTask?.Cancel(); } catch { }
        _memoryDownloadTask = null;
        _scheduledSource = null;

        if (ui != null &&
            ui.rootVisualElement != null)
        {
            ui_ButtonSelect!.clickable = null;
            ui_ButtonCompile!.clickable = null;
            ui_ButtonHalt!.clickable = null;
            ui_ButtonReset!.clickable = null;
            ui_ButtonContinue!.clickable = null;
            ui_labelTerminal!.text = string.Empty;
            EndFileSelection();
        }
        gameObject.SetActive(false);
    }
}
