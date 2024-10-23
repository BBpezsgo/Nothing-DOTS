using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

#nullable enable

public class TerminalManager : Singleton<TerminalManager>
{
    [SerializeField, NotNull] UIDocument? UI = default;

    Entity selectedUnitEntity = Entity.Null;
    float refreshAt = default;
    string[]? selectingFile = null;
    int selectingFileI = 0;

    Button? ui_ButtonSelect;
    Button? ui_ButtonCompile;
    Label? ui_labelTerminal;
    TextField? ui_inputSourcePath;

    void Update()
    {
        if (!UI.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            CloseUI();
            return;
        }

        if (Time.time >= refreshAt || selectingFile != null)
        {
            RefreshUI(selectedUnitEntity);
            refreshAt = Time.time + .2f;
            return;
        }
    }

    public void OpenUI(Entity unitEntity)
    {
        UIManager.CloseAllPopupUI();

        UI.gameObject.SetActive(true);
        selectedUnitEntity = unitEntity;
        refreshAt = Time.time + .2f;
        selectingFile = null;
        selectingFileI = 0;

        ui_inputSourcePath = UI.rootVisualElement.Q<TextField>("input-source-path");
        ui_ButtonSelect = UI.rootVisualElement.Q<Button>("button-select");
        ui_ButtonCompile = UI.rootVisualElement.Q<Button>("button-compile");
        ui_labelTerminal = UI.rootVisualElement.Q<Label>("label-terminal");

        {
            EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
            Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
            ui_inputSourcePath!.value = processor.SourceFile.Name.ToString();
        }

        BlurTerminal();

        ui_ButtonSelect.clickable = new Clickable(() =>
        {
            selectingFile = Directory.GetFiles(FileChunkManager.BasePath).Select(v => Path.GetRelativePath(FileChunkManager.BasePath, v)).ToArray();
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
        if (selectingFile != null)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                selectingFile = null;
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
                    selectingFile = null;
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
            if (processor.CompilerCache == Entity.Null)
            {
                ui_labelTerminal!.text = "No source";
            }
            else
            {
                CompilerCache compilerCache = entityManager.GetComponentData<CompilerCache>(processor.CompilerCache);

                if (compilerCache.CompileSecuedued != default)
                {
                    ui_labelTerminal!.text = "Compile secuedued ...";
                }
                else
                {
                    ui_labelTerminal!.text = processor.StdOutBuffer.ToString();
                }
            }
        }
    }

    public void CloseUI()
    {
        UI.gameObject.SetActive(false);
        selectedUnitEntity = Entity.Null;
        refreshAt = float.PositiveInfinity;
        selectingFile = null;
        selectingFileI = 0;

        if (UI.rootVisualElement != null)
        {
            ui_ButtonSelect!.clickable = null;
            ui_ButtonCompile!.clickable = null;
            ui_labelTerminal!.text = string.Empty;
            BlurTerminal();
        }
    }
}
