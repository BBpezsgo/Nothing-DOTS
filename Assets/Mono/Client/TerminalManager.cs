using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using LanguageCore;
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
    [SerializeField, NotNull] VisualTreeAsset? ProgressItem = default;
    [SerializeField, NotNull] VisualTreeAsset? DiagnosticsItem = default;
    [SerializeField, NotNull] Texture2D? DiagnosticsErrorIcon = default;
    [SerializeField, NotNull] Texture2D? DiagnosticsWarningIcon = default;
    [SerializeField, NotNull] Texture2D? DiagnosticsInfoIcon = default;
    [SerializeField, NotNull] Texture2D? DiagnosticsHintIcon = default;
    [SerializeField, NotNull] Texture2D? DiagnosticsOptimizationNoticeIcon = default;
    [SerializeField, ReadOnly] UIDocument? ui = default;

    [NotNull] Button? ui_ButtonSelect = default;
    [NotNull] Button? ui_ButtonCompile = default;
    [NotNull] Button? ui_ButtonHalt = default;
    [NotNull] Button? ui_ButtonReset = default;
    [NotNull] Button? ui_ButtonContinue = default;
    [NotNull] Label? ui_labelTerminal = default;

    [NotNull] ScrollView? ui_scrollTerminal = default;
    [NotNull] ScrollView? ui_scrollFiles = default;
    [NotNull] ScrollView? ui_scrollProgresses = default;
    [NotNull] ScrollView? ui_scrollDiagnostics = default;
    [NotNull] TabView? ui_tabView = default;

    [NotNull] TextField? ui_inputSourcePath = default;
    [NotNull] ProgressBar? ui_progressCompilation = default;

    readonly StringBuilder _terminalBuilder = new();
    TerminalEmulator? _terminal;
    byte[]? _memory;
    ProgressRecord<(int, int)>? _memoryDownloadProgress;
    Awaitable<RemoteFile>? _memoryDownloadTask;
    string? _scheduledSource;
    Tab _requestedTabSwitch;
    Tab? _fulfilledTabSwitch;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        if (!_fulfilledTabSwitch.HasValue || _fulfilledTabSwitch.Value != _requestedTabSwitch)
        {
            ui_tabView.selectedTabIndex = (int)_requestedTabSwitch;
            _fulfilledTabSwitch = _requestedTabSwitch;
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
        ui_scrollProgresses = ui.rootVisualElement.Q<ScrollView>("scroll-progresses");
        ui_scrollDiagnostics = ui.rootVisualElement.Q<ScrollView>("scroll-diagnostics");
        ui_tabView = ui.rootVisualElement.Q<TabView>("tabs");
        ui_progressCompilation = ui.rootVisualElement.Q<ProgressBar>("progress-compilation");

        ui_labelTerminal.text = string.Empty;
        ui_scrollFiles.Clear();
        ui_scrollProgresses.Clear();
        ui_scrollDiagnostics.Clear();

        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            ui_inputSourcePath.value = processor.SourceFile.Name.ToString();
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

            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                typeof(SendRpcCommandRequest),
                typeof(SetProcessorSourceRequestRpc),
            });
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

            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                typeof(SendRpcCommandRequest),
                typeof(ProcessorCommandRequestRpc),
            });
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

            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                typeof(SendRpcCommandRequest),
                typeof(ProcessorCommandRequestRpc),
            });
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

            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                typeof(SendRpcCommandRequest),
                typeof(ProcessorCommandRequestRpc),
            });
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
        ui_ButtonCompile.SetEnabled(false);
        ui_ButtonHalt.SetEnabled(false);
        ui_ButtonReset.SetEnabled(false);
        ui_ButtonSelect.SetEnabled(false);
        ui_ButtonContinue.SetEnabled(false);
    }

    void EndFileSelection()
    {
        ui_ButtonCompile.SetEnabled(true);
        ui_ButtonSelect.SetEnabled(true);
        ui_ButtonHalt.SetEnabled(true);
        ui_ButtonReset.SetEnabled(true);
        ui_ButtonContinue.SetEnabled(true);
        ui_scrollFiles.Clear();
    }

    static readonly string[] ProgressStatusClasses = new string[]
    {
        "error",
        "warning",
        "success",
    };

    void RefreshFileList()
    {
        ui_scrollFiles.SyncList(selectingFile, FileItem, (file, element, recycled) =>
        {
            element.userData = file;
            element.Q<Button>().text = file;
            if (!recycled) element.Q<Button>().clicked += () =>
            {
                ui_inputSourcePath.value = (string)element.userData;
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
    }

    enum Tab
    {
        Terminal,
        Diagnostics,
        Progress,
        Files,
    }

    public unsafe void RefreshUI(Entity unitEntity)
    {
        if (!selectingFile.IsEmpty)
        {
            RefreshFileList();
            return;
        }

        bool isBottom = ui_scrollTerminal.scrollOffset == ui_labelTerminal.layout.max - ui_scrollTerminal.contentViewport.layout.size;
        _terminalBuilder.Clear();

        void SetProgressStatus(string? status)
        {
            foreach (string item in ProgressStatusClasses)
            {
                ui_progressCompilation.EnableInClassList(item, item == status);
            }
        }

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
        SerializableDictionary<FileId, CompiledSource> compiledSources = ConnectionManager.ClientOrDefaultWorld.GetExistingSystemManaged<CompilerSystemClient>().CompiledSources;
        if (processor.SourceFile == default || !compiledSources.TryGetValue(processor.SourceFile, out CompiledSource? source))
        {
            _terminal = null;
            if (_scheduledSource != null)
            {
                ui_progressCompilation.title = "Scheduled ...";
                ui_progressCompilation.value = 0f;
                SetProgressStatus(null);
            }
            else
            {
                ui_progressCompilation.title = "No source";
                ui_progressCompilation.value = 0f;
                SetProgressStatus(null);
            }
        }
        else
        {
            _scheduledSource = null;
            const string SpinnerChars = "-\\|/";
            char spinner = SpinnerChars[(int)(MonoTime.Now * 8f) % SpinnerChars.Length];

            void SyncDiagnosticItems(VisualElement container, IEnumerable<Diagnostic> diagnostics)
            {
                container.SyncList(
                    diagnostics
                        .Where(v => v.Level != DiagnosticsLevel.OptimizationNotice)
                        .ToArray(),
                    DiagnosticsItem,
                    (item, element, recycled) =>
                    {
                        element.userData = item;
                        VisualElement icon = element.Q<VisualElement>("diagnostic-icon");
                        Label label = element.Q<Label>("diagnostic-label");
                        Foldout foldout = element.Q<Foldout>("diagnostic-foldout");

                        icon.style.backgroundImage = item.Level switch
                        {
                            DiagnosticsLevel.Error => new StyleBackground(DiagnosticsErrorIcon),
                            DiagnosticsLevel.Warning => new StyleBackground(DiagnosticsWarningIcon),
                            DiagnosticsLevel.Information => new StyleBackground(DiagnosticsInfoIcon),
                            DiagnosticsLevel.Hint => new StyleBackground(DiagnosticsHintIcon),
                            DiagnosticsLevel.OptimizationNotice => new StyleBackground(DiagnosticsOptimizationNoticeIcon),
                            _ => new StyleBackground(DiagnosticsInfoIcon),
                        };

                        label.text = item.Message;
                        if (item.SubErrors.Length > 0)
                        {
                            SyncDiagnosticItems(foldout, item.SubErrors);
                        }
                        else
                        {
                            foldout.style.display = DisplayStyle.None;
                        }
                    });
            }
            SyncDiagnosticItems(ui_scrollDiagnostics!, source.Diagnostics.Diagnostics);

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
                            ui_progressCompilation.title = "Compiling ...";
                            ui_progressCompilation.value = 0f;
                            SetProgressStatus(null);
                        }
                        else
                        {
                            ui_progressCompilation.title = "Secuedued ...";
                            ui_progressCompilation.value = 0f;
                            SetProgressStatus(null);
                        }
                        break;
                    case CompilationStatus.Compiling:
                        ui_progressCompilation.title = "Compiling ...";
                        ui_progressCompilation.value = 0f;
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

                ui_scrollProgresses.SyncList(source.SubFiles.ToArray(), ProgressItem, (file, element, recycled) =>
                {
                    ProgressBar progressBar = element.Q<ProgressBar>();
                    progressBar.title = file.Key.Name.ToString();
                    if (file.Value.Progress.Total == 0)
                    {
                        progressBar.value = 0f;
                    }
                    else
                    {
                        progressBar.value = (float)file.Value.Progress.Current / (float)file.Value.Progress.Total;
                    }
                });

                ui_progressCompilation.title = "Uploading ...";
                ui_progressCompilation.value = source.Progress;
                SetProgressStatus(null);

                _requestedTabSwitch = Tab.Progress;
            }

            switch (source.Status)
            {
                case CompilationStatus.Secuedued:
                    {
                        _terminal = null;
                        if (float.IsNaN(source.Progress))
                        {
                            ui_progressCompilation.title = "Compilation soon ...";
                            SetProgressStatus(null);
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
                        ui_progressCompilation.title = "Compiled";
                        ui_progressCompilation.value = 1f;
                        SetProgressStatus(null);
                        break;
                    }
                case CompilationStatus.Done:
                    {
                        if (source.IsSuccess)
                        {
                            ui_progressCompilation.title = "Running";
                            ui_progressCompilation.value = 1f;
                            SetProgressStatus("success");
                            _terminal ??= new TerminalEmulator(ui_labelTerminal);
                            _terminal.Update();
                            _requestedTabSwitch = Tab.Terminal;
                            _terminalBuilder.Append(processor.StdOutBuffer.ToString());

                            switch (processor.Signal)
                            {
                                case Signal.None:
                                    _memory = null;
                                    _memoryDownloadProgress = null;
                                    // try { _memoryDownloadTask?.Cancel(); } catch { }
                                    _memoryDownloadTask = null;
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
                                            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
                                            {
                                                typeof(SendRpcCommandRequest),
                                                typeof(ProcessorCommandRequestRpc),
                                            });
                                            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
                                            world.EntityManager.SetComponentData(entity, new ProcessorCommandRequestRpc()
                                            {
                                                Entity = ghostInstance,
                                                Command = ProcessorCommand.Key,
                                                Data = unchecked((ushort)c),
                                            });
                                        }
                                    }
                                    else if (ui_labelTerminal.panel.focusController.focusedElement == ui_labelTerminal && Time.time % 1f < .5f)
                                    {
                                        _terminalBuilder.Append("<mark=#ffffffff>_</mark>");
                                    }
                                    break;
                                case Signal.UserCrash:
                                    ui_progressCompilation.title = "Crashed";
                                    ui_progressCompilation.value = 1f;
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

                                        Awaitable<RemoteFile>.Awaiter awaiter = _memoryDownloadTask.GetAwaiter();
                                        if (awaiter.IsCompleted)
                                        {
                                            // Debug.Log("Memory loaded");
                                            RemoteFile result = awaiter.GetResult();
                                            switch (result.Kind)
                                            {
                                                case FileResponseStatus.NotFound:
                                                    ui_progressCompilation.title = "Crashed (no memory)";
                                                    ui_progressCompilation.value = 1f;
                                                    break;
                                                default:
                                                    _memory = result.File.Data;
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            ui_progressCompilation.title = "Crashed (loading memory)";
                                            ui_progressCompilation.value = (float)_memoryDownloadProgress.Progress.Item1 / (float)_memoryDownloadProgress.Progress.Item2;
                                        }
                                    }
                                    else
                                    {
                                        string? message = HeapUtils.GetString(_memory, processor.Crash);
                                        _terminalBuilder.AppendLine();
                                        _terminalBuilder.Append("<color=red>");
                                        _terminalBuilder.Append(message);
                                        _terminalBuilder.Append("</color>");
                                        _terminalBuilder.AppendLine();
                                    }
                                    break;
                                case Signal.StackOverflow:
                                    ui_progressCompilation.title = "Stack overflow";
                                    ui_progressCompilation.value = 1f;
                                    SetProgressStatus("error");
                                    break;
                                case Signal.Halt:
                                    ui_progressCompilation.title = "Halted";
                                    ui_progressCompilation.value = 1f;
                                    SetProgressStatus("warning");
                                    break;
                                case Signal.UndefinedExternalFunction:
                                    ui_progressCompilation.title = "Runtime Error";
                                    ui_progressCompilation.value = 1f;
                                    SetProgressStatus("error");
                                    _terminalBuilder.AppendLine();
                                    _terminalBuilder.AppendLine($"<color=red>Undefined external function {processor.Crash}</color>");
                                    break;
                            }
                        }
                        else
                        {
                            ui_progressCompilation.title = "Compile failed";
                            SetProgressStatus("error");
                            _terminal = null;

                            _requestedTabSwitch = Tab.Diagnostics;

                            /*
                            foreach (Diagnostic item in source.Diagnostics.Diagnostics)
                            {
                                if (item.Level is
                                    DiagnosticsLevel.Hint or
                                    DiagnosticsLevel.OptimizationNotice or
                                    DiagnosticsLevel.Information)
                                { continue; }

                                _terminalBuilder.AppendLine(item.ToString());
                                (string SourceCode, string Arrows)? arrows = item.GetArrows(new ISourceProvider[]
                                {
                                    new NetcodeSourceProviderOffline(),
                                });
                                if (arrows.HasValue)
                                {
                                    _terminalBuilder.AppendLine(arrows.Value.SourceCode);
                                    _terminalBuilder.AppendLine(arrows.Value.Arrows);
                                }
                            }
                            foreach (DiagnosticWithoutContext item in source.Diagnostics.DiagnosticsWithoutContext)
                            {
                                _terminalBuilder.AppendLine(item.ToString());
                            }
                            */
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

        if (ui_tabView.selectedTabIndex == (int)Tab.Terminal)
        {
            ui_labelTerminal.text = _terminalBuilder.ToString();

            if (isBottom)
            {
                ui_scrollTerminal.scrollOffset = ui_labelTerminal.layout.max - ui_scrollTerminal.contentViewport.layout.size;
            }
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
            ui_ButtonSelect.clickable = null;
            ui_ButtonCompile.clickable = null;
            ui_ButtonHalt.clickable = null;
            ui_ButtonReset.clickable = null;
            ui_ButtonContinue.clickable = null;
            ui_labelTerminal.text = string.Empty;
            EndFileSelection();
        }
        gameObject.SetActive(false);
    }
}
