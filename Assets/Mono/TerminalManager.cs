using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
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
    int selectingFileI = 0;

    [Header("UI")]
    [SerializeField, ReadOnly] Button? ui_ButtonSelect;
    [SerializeField, ReadOnly] Button? ui_ButtonCompile;
    [SerializeField, ReadOnly] Label? ui_labelTerminal;
    [SerializeField, ReadOnly] TextField? ui_inputSourcePath;
    [SerializeField, ReadOnly] UIDocument? ui = default;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            Cleanup(ui);
            return;
        }

        if (Time.time >= refreshAt || selectingFile != null)
        {
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
        ui_labelTerminal = ui.rootVisualElement.Q<Label>("label-terminal");

        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            ui_inputSourcePath!.value = processor.SourceFile.Name.ToString();
        }

        BlurTerminal();

        ui_ButtonSelect.clickable = new Clickable(() =>
        {
            selectingFile = Directory.GetFiles(FileChunkManager.BasePath)
                .Select(v => Path.GetRelativePath(FileChunkManager.BasePath, v))
                .Where(v => !v.EndsWith(".meta"))
                .ToImmutableArray();
            selectingFileI = 0;
            SelectTerminal();
        });

        ui_ButtonCompile.clickable = new Clickable(() =>
        {
            World world = ConnectionManager.ClientOrDefaultWorld;

            if (world.IsServer())
            {
                Debug.LogError($"Not implemented");
                return;
            }

            Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(SetProcessorSourceRequestRpc));
            GhostInstance ghostInstance = world.EntityManager.GetComponentData<GhostInstance>(unitEntity);
            world.EntityManager.SetComponentData(entity, new SetProcessorSourceRequestRpc()
            {
                Source = ui_inputSourcePath.value,
                Entity = ghostInstance,
            });
        });

        RefreshUI(unitEntity);
    }

    void SelectTerminal()
    {
        ui_inputSourcePath!.tabIndex = -1;
        ui_ButtonCompile!.tabIndex = -1;
        ui_ButtonSelect!.tabIndex = -1;
        ui_labelTerminal!.tabIndex = -1;

        ui_ButtonCompile!.SetEnabled(false);
        ui_ButtonSelect!.SetEnabled(false);

        ui_inputSourcePath!.focusable = false;
        ui_ButtonCompile!.focusable = false;
        ui_ButtonSelect!.focusable = false;
        ui_labelTerminal!.focusable = false;

        ui_labelTerminal!.Focus();
    }

    void BlurTerminal()
    {
        ui_inputSourcePath!.tabIndex = 0;
        ui_ButtonCompile!.tabIndex = 0;
        ui_ButtonSelect!.tabIndex = 0;
        ui_labelTerminal!.tabIndex = 0;

        ui_ButtonCompile!.SetEnabled(true);
        ui_ButtonSelect!.SetEnabled(true);

        ui_inputSourcePath!.focusable = true;
        ui_ButtonCompile!.focusable = true;
        ui_ButtonSelect!.focusable = true;
        ui_labelTerminal!.focusable = true;
    }

    public void RefreshUI(Entity unitEntity)
    {
        if (!selectingFile.IsEmpty)
        {
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

                StringBuilder builder = new();
                for (int i = 0; i < selectingFile.Length; i++)
                {
                    if (selectingFileI == i) builder.Append("> ");
                    else builder.Append("  ");
                    builder.Append(selectingFile[i]);
                    builder.AppendLine();
                }
                ui_labelTerminal!.text = builder.ToString();
            }
        }
        else
        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            if (processor.SourceFile == default)
            {
                ui_labelTerminal!.text = "No source";
            }
            else
            {
                CompiledSource source = CompilerManager.Instance.CompiledSources[processor.SourceFile];
                switch (source.Status)
                {
                    case CompilationStatus.Secuedued:
                    case CompilationStatus.Compiling:
                        {
                            if (source.Progress == 0f &&
                                source.Status == CompilationStatus.Secuedued)
                            {
                                if (source.CompileSecuedued != default)
                                {
                                    ui_labelTerminal!.text = $"Compilation secuedued ...";
                                }
                                else
                                {
                                    ui_labelTerminal!.text = $"Compilation in {source.CompileSecuedued - Time.time:#.00} sec";
                                }
                                break;
                            }
                            const int progressBarWidth = 10;
                            string progressBar = new('#', (int)(source.Progress * progressBarWidth));
                            ui_labelTerminal!.text = $"Uploading {progressBar}{new string('_', progressBarWidth - progressBar.Length)}";
                            break;
                        }
                    case CompilationStatus.Compiled:
                        {
                            ui_labelTerminal!.text = "Compiled ...";
                            break;
                        }
                    case CompilationStatus.Done:
                        {
                            if (source.IsSuccess)
                            {
                                ui_labelTerminal!.text = processor.StdOutBuffer.ToString();
                            }
                            else
                            {
                                ui_labelTerminal!.text = "Compile failed";
                            }
                            break;
                        }
                    case CompilationStatus.None:
                    default:
                        {
                            ui_labelTerminal!.text = $"???";
                            break;
                        }
                }
            }
        }
    }

    public void Cleanup(UIDocument ui)
    {
        selectedUnitEntity = Entity.Null;
        refreshAt = float.PositiveInfinity;
        selectingFile = ImmutableArray<string>.Empty;
        selectingFileI = 0;

        if (ui != null &&
            ui.rootVisualElement != null)
        {
            ui_ButtonSelect!.clickable = null;
            ui_ButtonCompile!.clickable = null;
            ui_labelTerminal!.text = string.Empty;
            BlurTerminal();
        }
        gameObject.SetActive(false);
    }
}
