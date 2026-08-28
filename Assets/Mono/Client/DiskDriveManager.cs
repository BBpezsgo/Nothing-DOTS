using System;
using System.Text;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class DiskDriveManager : Singleton<DiskDriveManager>, IUISetup<Entity>, IUICleanup
{
    [Header("UI")]

    DiskDriveSchema? ui;

    Pendrive selected;

    void Update()
    {
        if (!ui.IsVisible()) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }
    }

    public void Setup(UIElementReference ui, Entity entity)
    {
        this.ui = new(ui.Element);
        RefreshUI(entity);
    }

    public void RefreshUI(Entity entity)
    {
        if (!ui.IsVisible()) return;

        selected = ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<Pendrive>(entity);

        Label labelHex = ui.LabelHex;
        Label labelAscii = ui.LabelAscii;

        StringBuilder builderHex = new();
        StringBuilder builderAscii = new();

        int until = 0;
        for (int i = selected.Span.Length - 1; i >= 0; i--)
        {
            if (selected.Span[i] != 0)
            {
                until = i + 1;
                break;
            }
        }

        for (int i = 0; i <= until; i++)
        {
            if (i > 0) builderHex.Append(' ');
            builderHex.Append(Convert.ToString(selected.Span[i], 16).PadLeft('0'));

            builderAscii.Append((char)selected.Span[i] switch
            {
                '\0' or '\b'
                    => '.',
                '\n' or '\r' or '\t'
                    => ' ',
                _ => (char)selected.Span[i],
            });
        }

        labelHex.text = builderHex.ToString();
        labelAscii.text = builderAscii.ToString();
    }

    public void Cleanup(UIElementReference ui)
    {
        selected = default;
    }
}
