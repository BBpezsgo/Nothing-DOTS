using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

public class FactoryManager : Singleton<FactoryManager>, IUISetup<Entity>, IUICleanup
{
    [Header("UI Assets")]
    [SerializeField, NotNull] VisualTreeAsset? UI_AvaliableItem = default;
    [SerializeField, NotNull] VisualTreeAsset? UI_QueueItem = default;

    [Header("UI")]
    [SerializeField, ReadOnly] UIDocument? ui = default;

    Entity selectedFactoryEntity = Entity.Null;
    Factory selectedFactory = default;
    float refreshAt = default;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            Cleanup(ui);
            return;
        }

        if (Time.time >= refreshAt)
        {
            RefreshUI(selectedFactoryEntity);
            refreshAt = Time.time + 1f;
            return;
        }

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        selectedFactory = entityManager.GetComponentData<Factory>(selectedFactoryEntity);

        if (selectedFactory.TotalProgress == default) return;

        selectedFactory.CurrentProgress += Time.deltaTime * Factory.ProductionSpeed;
        ui.rootVisualElement.Q<ProgressBar>("progress-current").value = selectedFactory.CurrentProgress / selectedFactory.TotalProgress;
        ui.rootVisualElement.Q<ProgressBar>("progress-current").title = selectedFactory.Current.Name.ToString();
    }

    public void Setup(UIDocument ui, Entity factoryEntity)
    {
        gameObject.SetActive(true);
        this.ui = ui;

        ui.gameObject.SetActive(true);
        selectedFactoryEntity = factoryEntity;
        RefreshUI(factoryEntity);
    }

    public void RefreshUI(Entity factoryEntity)
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        ScrollView avaliableList = ui.rootVisualElement.Q<ScrollView>("list-avaliable");
        ScrollView queueList = ui.rootVisualElement.Q<ScrollView>("list-queue");

        avaliableList.Clear();
        queueList.Clear();

        DynamicBuffer<BufferedUnit> queue = entityManager.GetBuffer<BufferedUnit>(factoryEntity);

        queueList.SyncList(queue, UI_QueueItem, (item, element, recycled) =>
        {
            element.Q<Label>("label-unit-name").text = item.Name.ToString();
        });

        using EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
        if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedUnit> units = entityManager.GetBuffer<BufferedUnit>(buildingDatabase, true);

        avaliableList.SyncList(units, UI_AvaliableItem, (item, element, recycled) =>
        {
            element.userData = item.Name.ToString();
            element.Q<Label>("label-unit-name").text = item.Name.ToString();
            if (!recycled) element.Q<Button>("button-queue").clicked += () => QueueUnit((string)element.userData);
        });

        ui.rootVisualElement.Q<ProgressBar>("progress-current").value = selectedFactory.CurrentProgress / selectedFactory.TotalProgress;
        ui.rootVisualElement.Q<ProgressBar>("progress-current").title = selectedFactory.Current.Name.ToString();
    }

    void QueueUnit(string unitName)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        using EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
        if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity unitDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedUnit> units = entityManager.GetBuffer<BufferedUnit>(unitDatabase, true);

        BufferedUnit unit = units.FirstOrDefault(v => v.Name == unitName);

        if (unit.Prefab == Entity.Null)
        {
            Debug.LogWarning($"Unit \"{unitName}\" not found in the database");
            return;
        }

        GhostInstance ghostInstance = entityManager.GetComponentData<GhostInstance>(selectedFactoryEntity);

        Entity entity = entityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(FactoryQueueUnitRequestRpc));
        entityManager.SetComponentData(entity, new FactoryQueueUnitRequestRpc()
        {
            Unit = unit.Name,
            FactoryEntity = ghostInstance,
        });

        if (selectedFactory.TotalProgress == default)
        {
            selectedFactory.Current = unit;
            selectedFactory.CurrentProgress = 0f;
            selectedFactory.TotalProgress = unit.ProductionTime;
        }
        else
        {
            DynamicBuffer<BufferedUnit> queue = entityManager.GetBuffer<BufferedUnit>(selectedFactoryEntity);
            queue.Add(unit);
        }
        refreshAt = Time.time + .1f;
    }

    public void Cleanup(UIDocument ui)
    {
        selectedFactoryEntity = Entity.Null;
        selectedFactory = default;
        refreshAt = float.PositiveInfinity;
        gameObject.SetActive(false);
    }
}
