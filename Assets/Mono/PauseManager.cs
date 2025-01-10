using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using ReadOnlyAttribute = NaughtyAttributes.ReadOnlyAttribute;

public class PauseManager : Singleton<PauseManager>, IUISetup, IUICleanup
{
    [Header("UI Assets")]

    [SerializeField, NotNull] VisualTreeAsset? UI_ConnectionItem = default;

    [Header("UI")]

    [SerializeField, ReadOnly] UIDocument? ui = default;

    float refreshAt = default;

    void Update()
    {
        if (UIManager.Instance.GrapESC())
        {
            if (ui == null || !ui.gameObject.activeSelf)
            {
                UIManager.Instance.OpenUI(UIManager.Instance.Pause)
                    .Setup(this);
            }
            else
            {
                UIManager.Instance.CloseUI(this);
            }
            return;
        }

        if (ui == null || !ui.gameObject.activeSelf) return;

        if (Time.time >= refreshAt)
        {
            RefreshUI();
            refreshAt = Time.time + 1f;
        }
    }

    public void Setup(UIDocument ui)
    {
        this.ui = ui;
        refreshAt = 0f;

        ui.gameObject.SetActive(true);
        ui.rootVisualElement.Q<Button>("button-exit").clicked += OnButtonExit;
    }

    void OnButtonExit()
    {
        ConnectionManager.DisconnectEveryone();
        Application.Quit();
    }

    public void RefreshUI()
    {
        if (ui == null || !ui.gameObject.activeSelf || ConnectionManager.ClientOrDefaultWorld == null) return;

        ScrollView connectionsList = ui.rootVisualElement.Q<ScrollView>("list-connections");

        connectionsList.Clear();

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        using EntityQuery playersQ = entityManager.CreateEntityQuery(typeof(Player));
        using NativeArray<Player> players = playersQ.ToComponentDataArray<Player>(Allocator.Temp);

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player.ConnectionState == PlayerConnectionState.Disconnected) continue;
            TemplateContainer newItem = UI_ConnectionItem.Instantiate();
            newItem.Q<Label>().text = player.ConnectionId.ToString();
            newItem.Q<Button>("button-kick").clicked += () =>
            {
                ConnectionManager.KickClient(player.ConnectionId);
                RefreshUI();
            };
            newItem.Q<Button>("button-kick").SetEnabled(
                ConnectionManager.ServerWorld != null &&
                player.ConnectionId != 0
            );
            connectionsList.Add(newItem);
        }
    }

    public void Cleanup(UIDocument ui)
    {
        refreshAt = float.PositiveInfinity;
        ui.rootVisualElement.Q<Button>("button-exit").clicked -= OnButtonExit;
    }
}
