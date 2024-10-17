using System.Diagnostics.CodeAnalysis;
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

        foreach (BufferedUnit queueItem in queue)
        {
            TemplateContainer newItem = UI_QueueItem.Instantiate();
            newItem.Q<Label>("label-unit-name").text = queueItem.Name.ToString();
            queueList.Add(newItem);
        }

        EntityQuery unitDatabaseQ = entityManager.CreateEntityQuery(typeof(UnitDatabase));
        if (!unitDatabaseQ.TryGetSingletonEntity<UnitDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(UnitDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedUnit> units = entityManager.GetBuffer<BufferedUnit>(buildingDatabase, true);

        foreach (BufferedUnit unit in units)
        {
            TemplateContainer newItem = UI_AvaliableItem.Instantiate();
            newItem.Q<Label>("label-unit-name").text = unit.Name.ToString();
            newItem.Q<Button>("button-queue").clicked += () =>
            {
                entityManager.GetBuffer<BufferedUnit>(factoryEntity).Add(unit);
                RefreshUI(factoryEntity);
            };
            avaliableList.Add(newItem);
        }
    }

    public void CloseUI()
    {
        UI.gameObject.SetActive(false);
        selectedFactory = Entity.Null;
    }
}
