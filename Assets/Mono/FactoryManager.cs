using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

public class FactoryManager : Singleton<FactoryManager>
{
    [SerializeField, NotNull] UIDocument? UI = default;
    [SerializeField, NotNull] VisualTreeAsset? UI_AvaliableItem = default;
    [SerializeField, NotNull] VisualTreeAsset? UI_QueueItem = default;

    Entity selectedFactory = Entity.Null;

    void Update()
    {
        if (!UI.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            CloseUI();
            return;
        }
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Factory factory = entityManager.GetComponentData<Factory>(selectedFactory);

        if (factory.CurrentFinishAt == default) return;

        float now = Time.time;
        float totalDuration = factory.CurrentFinishAt - factory.CurrentStartedAt;
        float elapsedTime = now - factory.CurrentStartedAt;
        float progress = elapsedTime / totalDuration;
        ProgressBar progressCurrent = UI.rootVisualElement.Q<ProgressBar>("progress-current");
        progressCurrent.value = progress;
    }

    public void OpenUI(Entity factoryEntity)
    {
        UI.gameObject.SetActive(true);
        selectedFactory = factoryEntity;
        RefreshUI(factoryEntity);
    }

    public void RefreshUI(Entity factoryEntity)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        ScrollView avaliableList = UI.rootVisualElement.Q<ScrollView>("list-avaliable");
        ScrollView queueList = UI.rootVisualElement.Q<ScrollView>("list-queue");

        avaliableList.Clear();
        queueList.Clear();

        DynamicBuffer<BufferedUnit> queue = entityManager.GetBuffer<BufferedUnit>(factoryEntity);

        queueList.SyncList(queue, UI_QueueItem, (item, element, recycled) =>
        {
            element.Q<Label>("label-unit-name").text = item.Name.ToString();
        });

        EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
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
    }

    void QueueUnit(string unitName)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
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

        entityManager.GetBuffer<BufferedUnit>(selectedFactory).Add(unit);
        RefreshUI(selectedFactory);
    }

    public void CloseUI()
    {
        UI.gameObject.SetActive(false);
        selectedFactory = Entity.Null;
    }
}
