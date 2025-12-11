using System;
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
    [NotNull] Button? ui_ButtonHotReload = default;
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
    (Tab Condition, Tab Target) _requestedTabSwitch;
    Tab? _fulfilledTabSwitch;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        if (!_fulfilledTabSwitch.HasValue || _fulfilledTabSwitch.Value != _requestedTabSwitch.Target)
        {
            if ((int)_requestedTabSwitch.Condition == ui_tabView.selectedTabIndex)
            {
                ui_tabView.selectedTabIndex = (int)_requestedTabSwitch.Target;
            }
            _fulfilledTabSwitch = _requestedTabSwitch.Target;
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
        ui_ButtonHotReload = ui.rootVisualElement.Q<Button>("button-hotreload");
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

            NetcodeUtils.CreateRPC(world.Unmanaged, new SetProcessorSourceRequestRpc()
            {
                Source = ui_inputSourcePath.value,
                Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
                IsHotReload = false,
            });

            _scheduledSource = ui_inputSourcePath.value;
        });

        ui_ButtonHotReload.clickable = new Clickable(() =>
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

            NetcodeUtils.CreateRPC(world.Unmanaged, new SetProcessorSourceRequestRpc()
            {
                Source = ui_inputSourcePath.value,
                Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
                IsHotReload = true,
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

            NetcodeUtils.CreateRPC(world.Unmanaged, new ProcessorCommandRequestRpc()
            {
                Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
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

            NetcodeUtils.CreateRPC(world.Unmanaged, new ProcessorCommandRequestRpc()
            {
                Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
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

            NetcodeUtils.CreateRPC(world.Unmanaged, new ProcessorCommandRequestRpc()
            {
                Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
                Command = ProcessorCommand.Continue,
                Data = default,
            });
        });

        RefreshUI(unitEntity);
    }

    void BeginFileSelection()
    {
        ui_ButtonCompile.SetEnabled(false);
        ui_ButtonHotReload.SetEnabled(false);
        ui_ButtonHalt.SetEnabled(false);
        ui_ButtonReset.SetEnabled(false);
        ui_ButtonSelect.SetEnabled(false);
        ui_ButtonContinue.SetEnabled(false);
    }

    void EndFileSelection()
    {
        ui_ButtonCompile.SetEnabled(true);
        ui_ButtonHotReload.SetEnabled(true);
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
            if (!recycled)
            {
                element.Q<Button>().clicked += () =>
                {
                    ui_inputSourcePath.value = (string)element.userData;
                    selectingFile = ImmutableArray<string>.Empty;
                    EndFileSelection();
                };
            }
        });

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

    public void RefreshUI(Entity unitEntity)
    {
        if (!selectingFile.IsEmpty)
        {
            RefreshFileList();
            return;
        }

        bool isBottom = true; // ui_scrollTerminal.scrollOffset == ui_labelTerminal.layout.max - ui_scrollTerminal.contentViewport.layout.size;
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
        _terminalBuilder.Append(processor.StdOutBuffer.ToString());
        CompiledSourceClient? clientSource = null;
        CompiledSourceServer? serverSource = null;
        if (processor.SourceFile == default ||
            !(ConnectionManager.ClientOrDefaultWorld.IsClient() ? ConnectionManager.ClientOrDefaultWorld.GetExistingSystemManaged<CompilerSystemClient>().CompiledSources.TryGetValue(processor.SourceFile, out clientSource) : ConnectionManager.ClientOrDefaultWorld.GetExistingSystemManaged<CompilerSystemServer>().CompiledSources.TryGetValue(processor.SourceFile, out serverSource)))
        {
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
            ICompiledSource source = ((clientSource as ICompiledSource) ?? serverSource)!;

            _scheduledSource = null;
            const string SpinnerChars = "-\\|/";
            char spinner = SpinnerChars[(int)(MonoTime.Now * 8f) % SpinnerChars.Length];

            void SyncDiagnosticItems(VisualElement container, IEnumerable<IDiagnostic> diagnostics, DiagnosticsLevel parentLevel)
            {
                container.SyncList(
                    diagnostics
                        .Where(v => v.Level is not DiagnosticsLevel.OptimizationNotice and not DiagnosticsLevel.FailedOptimization)
                        .ToArray(),
                    DiagnosticsItem,
                    (item, element, recycled) =>
                    {
                        element.userData = item;
                        VisualElement icon = element.Q<VisualElement>("diagnostic-icon");
                        Label label = element.Q<Label>("diagnostic-label");
                        Foldout foldout = element.Q<Foldout>("diagnostic-foldout");
                        DiagnosticsLevel fixedLevel = item.Level > parentLevel ? item.Level : parentLevel;

                        icon.style.backgroundImage = fixedLevel switch
                        {
                            DiagnosticsLevel.Error => new StyleBackground(DiagnosticsErrorIcon),
                            DiagnosticsLevel.Warning => new StyleBackground(DiagnosticsWarningIcon),
                            DiagnosticsLevel.Information => new StyleBackground(DiagnosticsInfoIcon),
                            DiagnosticsLevel.Hint => new StyleBackground(DiagnosticsHintIcon),
                            DiagnosticsLevel.OptimizationNotice => new StyleBackground(DiagnosticsOptimizationNoticeIcon),
                            DiagnosticsLevel.FailedOptimization => new StyleBackground(DiagnosticsWarningIcon),
                            _ => new StyleBackground(DiagnosticsInfoIcon),
                        };

                        label.text = item.Message;
                        if (item.SubErrors.Any())
                        {
                            SyncDiagnosticItems(foldout, item.SubErrors, fixedLevel);
                        }
                        else
                        {
                            foldout.style.display = DisplayStyle.None;
                        }
                    });
            }
            SyncDiagnosticItems(ui_scrollDiagnostics, source.Diagnostics, default);

            if (source.Status != CompilationStatus.Done && !float.IsNaN(source.Progress))
            {
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

                ui_progressCompilation.value = source.Progress;

                _requestedTabSwitch = (Tab.Files, Tab.Progress);
            }

            switch (source.Status)
            {
                case CompilationStatus.Secuedued:
                {
                    ui_progressCompilation.title = "Secuedued ...";
                    SetProgressStatus(null);
                    break;
                }
                case CompilationStatus.Compiling:
                {
                    ui_progressCompilation.title = "Compiling ...";
                    SetProgressStatus(null);
                    break;
                }
                case CompilationStatus.Uploading:
                {
                    ui_progressCompilation.title = "Uploading ...";
                    SetProgressStatus(null);
                    break;
                }
                case CompilationStatus.Generating:
                {
                    ui_progressCompilation.title = "Generating ...";
                    SetProgressStatus(null);
                    break;
                }
                case CompilationStatus.Generated:
                {
                    ui_progressCompilation.title = "Generated";
                    SetProgressStatus(null);
                    break;
                }
                case CompilationStatus.Done:
                {
                    if (source.IsSuccess)
                    {
                        _terminal ??= new TerminalEmulator(ui_labelTerminal);
                        _terminal.Update();
                        _requestedTabSwitch = (Tab.Progress, Tab.Terminal);

                        switch (processor.Signal)
                        {
                            case Signal.None:
                                ui_progressCompilation.title = "Running";
                                ui_progressCompilation.value = 1f;
                                SetProgressStatus("success");
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
                                        NetcodeUtils.CreateRPC(world.Unmanaged, new ProcessorCommandRequestRpc()
                                        {
                                            Entity = world.EntityManager.GetComponentData<GhostInstance>(unitEntity),
                                            Command = ProcessorCommand.Key,
                                            Data = c,
                                        });
                                    }
                                }
                                else if (ui_labelTerminal.panel.focusController.focusedElement == ui_labelTerminal && Time.time % 1f < .5f)
                                {
                                    _terminalBuilder.Append("<mark=#ffffffff>_</mark>");
                                }
                                break;
                            case Signal.UserCrash:
                                ui_progressCompilation.title = "User-crashed";
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
                                            .RequestFile(new FileId($"/i/e/{ghostInstance.ghostId}.{ghostInstance.spawnTick.SerializedData}/m", NetcodeEndPoint.Server), _memoryDownloadProgress);
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
                                    _terminalBuilder.Append(message ?? "null");
                                    _terminalBuilder.Append("</color>");
                                    _terminalBuilder.AppendLine();
                                }
                                break;
                            case Signal.StackOverflow:
                                ui_progressCompilation.title = "Crashed";
                                ui_progressCompilation.value = 1f;
                                _terminalBuilder.AppendLine();
                                _terminalBuilder.AppendLine($"<color=red>Stack overflow</color>");
                                SetProgressStatus("error");
                                break;
                            case Signal.Halt:
                                ui_progressCompilation.title = "Halted";
                                ui_progressCompilation.value = 1f;
                                SetProgressStatus("warning");
                                break;
                            case Signal.UndefinedExternalFunction:
                                ui_progressCompilation.title = "Crashed";
                                ui_progressCompilation.value = 1f;
                                SetProgressStatus("error");
                                _terminalBuilder.AppendLine();
                                _terminalBuilder.AppendLine($"<color=red>Undefined external function {processor.Crash}</color>");
                                break;
                            case Signal.PointerOutOfRange:
                                ui_progressCompilation.title = "Crashed";
                                ui_progressCompilation.value = 1f;
                                SetProgressStatus("error");
                                _terminalBuilder.AppendLine();
                                _terminalBuilder.AppendLine($"<color=red>Pointer out of Range</color>");
                                break;
                            default:
                                throw new UnreachableException();
                        }
                    }
                    else
                    {
                        ui_progressCompilation.title = "Compile failed";
                        SetProgressStatus("error");

                        _requestedTabSwitch = (Tab.Progress, Tab.Diagnostics);
                    }
                    break;
                }
                case CompilationStatus.None:
                default: throw new UnreachableException();
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
            ui_ButtonHotReload.clickable = null;
            ui_ButtonHalt.clickable = null;
            ui_ButtonReset.clickable = null;
            ui_ButtonContinue.clickable = null;
            ui_labelTerminal.text = string.Empty;
            EndFileSelection();
        }
        gameObject.SetActive(false);
    }
}
