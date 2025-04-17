using System;
using System.Text;
using LanguageCore;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using ReadOnlyAttribute = NaughtyAttributes.ReadOnlyAttribute;

public class DiskDriveManager : Singleton<DiskDriveManager>, IUISetup<Entity>, IUICleanup
{
    [Header("UI")]

    [SerializeField, ReadOnly] UIDocument? ui = default;

    Entity selectedEntity = Entity.Null;
    Pendrive selected = default;

    void Update()
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        selected = entityManager.GetComponentData<Pendrive>(selectedEntity);
    }

    public void Setup(UIDocument ui, Entity entity)
    {
        gameObject.SetActive(true);
        this.ui = ui;
        ui.gameObject.SetActive(true);
        selectedEntity = entity;
        RefreshUI(entity);
    }

    public void RefreshUI(Entity entity)
    {
        if (ui == null || !ui.gameObject.activeSelf) return;

        var labelHex = ui.rootVisualElement.Q<Label>("label-hex");
        var labelAscii = ui.rootVisualElement.Q<Label>("label-ascii");

        StringBuilder builderHex = new();
        StringBuilder builderAscii = new();

        FixedBytes1024 _data = selected.Data;
        Span<byte> data;
        unsafe { data = new(&_data, 1024); }

        int until = 0;
        for (int i = data.Length - 1; i >= 0; i--)
        {
            if (data[i] != 0)
            {
                until = i + 1;
                break;
            }
        }

        for (int i = 0; i <= until; i++)
        {
            builderHex.Append(Convert.ToString(data[i], 16).PadLeft('0'));
            builderAscii.Append((char)data[i] switch
            {
                '\0' or '\b'
                    => '.',
                '\n' or '\r' or '\t'
                    => ' ',
                _ => (char)data[i],
            });
        }

        labelHex.text = builderHex.ToString();
        labelAscii.text = builderAscii.ToString();
    }

    public void Cleanup(UIDocument ui)
    {
        selectedEntity = Entity.Null;
        selected = default;
        gameObject.SetActive(false);
    }
}
