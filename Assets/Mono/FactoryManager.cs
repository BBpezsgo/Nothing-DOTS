using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

public class FactoryManager : Singleton<FactoryManager>
{
    [SerializeField, NotNull] UIDocument? UI = default;
    [SerializeField, NotNull] VisualTreeAsset? UI_AvaliableItem = default;
    [SerializeField, NotNull] VisualTreeAsset? UI_QueueItem = default;

    Entity selectedFactoryEntity = Entity.Null;
    Factory selectedFactory = default;
    float refreshAt = default;

    void Update()
    {
        if (!UI.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            CloseUI();
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
        UI.rootVisualElement.Q<ProgressBar>("progress-current").value = selectedFactory.CurrentProgress / selectedFactory.TotalProgress;
        UI.rootVisualElement.Q<ProgressBar>("progress-current").title = selectedFactory.Current.Name.ToString();
    }

    public void OpenUI(Entity factoryEntity)
    {
        UIManager.CloseAllPopupUI();

        UI.gameObject.SetActive(true);
        selectedFactoryEntity = factoryEntity;
        RefreshUI(factoryEntity);
    }

    public void RefreshUI(Entity factoryEntity)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        ScrollView avaliableList = UI.rootVisualElement.Q<ScrollView>("list-avaliable");
        ScrollView queueList = UI.rootVisualElement.Q<ScrollView>("list-queue");

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

        UI.rootVisualElement.Q<ProgressBar>("progress-current").value = selectedFactory.CurrentProgress / selectedFactory.TotalProgress;
        UI.rootVisualElement.Q<ProgressBar>("progress-current").title = selectedFactory.Current.Name.ToString();
    }

    void QueueUnit(string unitName)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        using EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
        if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedUnit> units = entityManager.GetBuffer<BufferedUnit>(buildingDatabase, true);

        BufferedUnit unit = units.FirstOrDefault(v => v.Name == unitName);

        if (unit.Prefab == Entity.Null)
        {
            Debug.LogWarning($"Unit \"{unitName}\" not found in the database");
            return;
        }

        var ghostInstance = entityManager.GetComponentData<GhostInstance>(selectedFactoryEntity);

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
        refreshAt = Time.time + .1f;
    }

    public void CloseUI()
    {
        UI.gameObject.SetActive(false);
        selectedFactoryEntity = Entity.Null;
        selectedFactory = default;
        refreshAt = float.PositiveInfinity;
    }
}
