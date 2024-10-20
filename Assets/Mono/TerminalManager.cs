using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

public class TerminalManager : Singleton<TerminalManager>
{
    [SerializeField, NotNull] UIDocument? UI = default;

    Entity selectedUnitEntity = Entity.Null;
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
        RefreshUI(unitEntity);
    }

    public void RefreshUI(Entity unitEntity)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        Processor processor = entityManager.GetComponentData<Processor>(unitEntity);
        UI.rootVisualElement.Q<Label>("label-terminal").text = processor.StdOutBuffer.ToString();
    }

    public void CloseUI()
    {
        UI.gameObject.SetActive(false);
        selectedUnitEntity = Entity.Null;
        refreshAt = float.PositiveInfinity;
        if (UI.rootVisualElement != null) UI.rootVisualElement.Q<Label>("label-terminal").text = string.Empty;
    }
}
