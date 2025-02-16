using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class WorldLabelSystemClientSystem : SystemBase
{
    Transform? _canvas;
    readonly List<(WorldLabel O, bool Enabled)> _instances = new();

    protected override void OnCreate()
    {
        RequireForUpdate<WorldLabels>();
    }

    protected override void OnUpdate()
    {
        if (_canvas == null)
        {
            if (_canvas == null)
            {
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace) continue;
                    _canvas = canvas.transform;
                    break;
                }
            }
        }

        WorldLabels config = SystemAPI.ManagedAPI.GetSingleton<WorldLabels>();
        DynamicBuffer<BufferedWorldLabel> labels = SystemAPI.GetSingletonBuffer<BufferedWorldLabel>(true);

        for (int i = System.Math.Min(labels.Length, _instances.Count) - 1; i >= 0; i--)
        {
            if (_instances[i].O.TextMeshPro.text != labels[i].Text) _instances[i].O.TextMeshPro.text = labels[i].Text.ToString();
            if (!_instances[i].Enabled)
            {
                _instances[i].O.gameObject.SetActive(true);
                _instances[i] = (_instances[i].O, true);
            }
            _instances[i].O.transform.position = labels[i].Position;
        }

        for (int i = _instances.Count - 1; i >= labels.Length; i--)
        {
            if (!_instances[i].Enabled) continue;
            _instances[i].O.gameObject.SetActive(false);
            _instances[i] = (_instances[i].O, false);
            // Object.Destroy(_instances[i].O.gameObject);
            // _instances.RemoveAt(i);
        }

        for (int i = _instances.Count; i < labels.Length; i++)
        {
            GameObject o = Object.Instantiate(config.Prefab, labels[i].Position, Quaternion.identity, _canvas);
            _instances.Add((o.GetComponent<WorldLabel>(), true));
        }
    }
}
