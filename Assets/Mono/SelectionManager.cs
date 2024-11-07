using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using RaycastHit = Unity.Physics.RaycastHit;

public class SelectionManager : Singleton<SelectionManager>
{
    [SerializeField] float BoxSelectDistanceThreshold = default;
    [SerializeField, NotNull] RectTransform? SelectBox = default;
    [SerializeField, NotNull] UIDocument? UnitCommandsUI = default;
    [SerializeField, NotNull] VisualTreeAsset? UnitCommandItemUI = default;

    bool _isSelectBoxVisible;
    Vector3 _selectionStart = default;
    HashSet<Entity> _selected = new();
    HashSet<Entity> _candidates = new();
    Entity _firstHit = Entity.Null;
    Vector3 _unitCommandUIWorldPosition = default;

    void SetSelectBoxVisible(bool visible)
    {
        if (_isSelectBoxVisible != visible) SelectBox.gameObject.SetActive(_isSelectBoxVisible = visible);
    }

    void Start()
    {
        _selected = new();
        _candidates = new();
    }

    void Update()
    {
        if (UI.IsMouseHandled)
        {
            SetSelectBoxVisible(false);
            _firstHit = Entity.Null;
            SelectBox.gameObject.SetActive(false);
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            BeginBoxSelect();
        }

        if (Input.GetMouseButtonUp(0))
        {
            FinishBoxSelect();
        }

        if (Input.GetMouseButtonDown(1))
        {
            BeginUnitAction();
        }

        if (Input.GetMouseButtonUp(1))
        {
            FinishUnitAction();
        }

        UpdateBoxSelect();

        if (UnitCommandsUI.isActiveAndEnabled)
        {
            DebugEx.DrawPoint(_unitCommandUIWorldPosition, 2f, Color.white);

            Vector3 screenPoint = MainCamera.Camera.WorldToScreenPoint(_unitCommandUIWorldPosition);
            if (screenPoint.z >= 0f)
            {
                screenPoint.z = 0f;
                screenPoint.y = MainCamera.Camera.pixelHeight - screenPoint.y;
                UnitCommandsUI.rootVisualElement.transform.position = screenPoint;
            }
        }

        // {
        //     UnityEngine.Plane plane = new(Vector3.up, Vector3.zero);
        //     UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        //     if (plane.Raycast(ray, out float distance))
        //     {
        //         var start = ray.GetPoint(distance);
        //         var dir = (-start).normalized;
        //         // QuadrantSystem.DrawQuadrant(ray.GetPoint(distance));
        //         NativeHashMap<uint, NativeList<QuadrantEntity>> map = QuadrantSystem.GetMap(World.DefaultGameObjectInjectionWorld.Unmanaged);
        //         if (QuadrantSystem.RayCast(map, start, default, out float t))
        //         {
        //             DebugEx.DrawPoint(start + dir * t, 2f, Color.cyan);
        //         }
        //     }
        // }
    }

    void BeginBoxSelect()
    {
        HideUnitCommandsUI();

        _selectionStart = default;

        var hit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Ground);
        if (hit.Entity == Entity.Null) return;

        _selectionStart = hit.Position;
    }

    void UpdateBoxSelect()
    {
        ClearSelectionCandidates();

        if (_selectionStart == default)
        {
            SetSelectBoxVisible(false);

            Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
            if (selectableHit == Entity.Null) return;
            SelectUnitCandidate(selectableHit, SelectionStatus.Candidate);

            return;
        }

        Vector3 startPoint = MainCamera.Camera.WorldToScreenPoint(_selectionStart);
        Vector3 endPoint = Input.mousePosition;

        if (startPoint.z <= 0f)
        {
            Debug.LogWarning($"Invalid selection box");
            SetSelectBoxVisible(false);
            return;
        }

        if (Vector2.Distance(startPoint, endPoint) < BoxSelectDistanceThreshold)
        {
            Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
            if (selectableHit == Entity.Null) return;
            SelectUnitCandidate(selectableHit, SelectionStatus.Candidate);
            SetSelectBoxVisible(false);
            return;
        }

        float minX = System.Math.Min(startPoint.x, endPoint.x);
        float minY = System.Math.Min(startPoint.y, endPoint.y);
        float maxX = System.Math.Max(startPoint.x, endPoint.x);
        float maxY = System.Math.Max(startPoint.y, endPoint.y);

        Rect rect = new(
            minX,
            minY,
            maxX - minX,
            maxY - minY
        );

        foreach (Entity unit in UnitsInRect(rect))
        { SelectUnitCandidate(unit, SelectionStatus.Candidate); }

        SelectBox.anchoredPosition = rect.position;
        SelectBox.sizeDelta = rect.size;
        SetSelectBoxVisible(true);
    }

    void FinishBoxSelect()
    {
        if (!Input.GetKey(KeyCode.LeftShift)) ClearSelection();
        SetSelectBoxVisible(false);

        if (_selectionStart == default) return;

        Vector3 startPoint = MainCamera.Camera.WorldToScreenPoint(_selectionStart);
        Vector3 endPoint = Input.mousePosition;
        _selectionStart = default;

        if (startPoint.z <= 0f)
        {
            Debug.LogWarning($"Invalid selection box");
            return;
        }

        if (Vector2.Distance(startPoint, endPoint) < BoxSelectDistanceThreshold)
        {
            Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
            if (selectableHit == Entity.Null) return;
            if (GetUnitStatus(selectableHit).Status == SelectionStatus.Selected &&
                _selected.Count > 0 &&
                Input.GetKey(KeyCode.LeftShift))
            { DeselectUnit(selectableHit); }
            else
            { SelectUnit(selectableHit, SelectionStatus.Selected); }
            return;
        }

        float minX = System.Math.Min(startPoint.x, endPoint.x);
        float minY = System.Math.Min(startPoint.y, endPoint.y);
        float maxX = System.Math.Max(startPoint.x, endPoint.x);
        float maxY = System.Math.Max(startPoint.y, endPoint.y);

        Rect rect = new(
            minX,
            minY,
            maxX - minX,
            maxY - minY
        );

        foreach (Entity unit in UnitsInRect(rect))
        { SelectUnit(unit, SelectionStatus.Selected); }
    }

    void BeginUnitAction()
    {
        _firstHit = Entity.Null;

        Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
        if (selectableHit == Entity.Null) return;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (!entityManager.HasComponent<SelectableUnit>(selectableHit))
        {
            Debug.LogError($"Entity {selectableHit} doesn't have a {nameof(SelectableUnit)} component");
            return;
        }

        _firstHit = selectableHit;
    }

    void FinishUnitAction()
    {
        if (_firstHit == Entity.Null)
        {
            ShowUnitCommandsUI();
            return;
        }

        Entity firstHit = _firstHit;
        _firstHit = Entity.Null;

        Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
        if (selectableHit == Entity.Null) return;

        if (selectableHit != firstHit) return;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (!entityManager.HasComponent<SelectableUnit>(selectableHit))
        {
            Debug.LogError($"Entity {selectableHit} doesn't have a {nameof(SelectableUnit)} component");
            return;
        }

        if (entityManager.HasComponent<Factory>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Factory)
                .Setup(FactoryManager.Instance, selectableHit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Unit>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }
    }

    void ShowUnitCommandsUI()
    {
        var hit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Ground);
        if (hit.Entity == Entity.Null) return;

        _unitCommandUIWorldPosition = hit.Position;
        UnitCommandsUI.gameObject.SetActive(true);

        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").style.display = DisplayStyle.None;
        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").value = 0f;

        VisualElement container = UnitCommandsUI.rootVisualElement.Q("container-unit-commands");
        container.Clear();

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        foreach (Entity selected in _selected)
        {
            DynamicBuffer<BufferedUnitCommandDefinition> commands = entityManager.GetBuffer<BufferedUnitCommandDefinition>(selected);

            for (int i = 0; i < commands.Length; i++)
            {
                string name = commands[i].Label.ToString();
                int id = commands[i].Id;
                bool added = false;

                foreach (var existingItemUi in container.Children())
                {
                    if (existingItemUi.userData is string _name &&
                        _name == name)
                    {
                        added = true;
                        break;
                    }
                }

                if (added) continue;

                TemplateContainer itemUi = UnitCommandItemUI.Instantiate();
                itemUi.Q<Button>("unit-command-name").text = $"#{id} {name}";
                itemUi.Q<Button>("unit-command-name").clicked += () => HandleUnitCommandClick(id);
                itemUi.userData = name;

                container.Add(itemUi);
            }
        }
    }

    void HideUnitCommandsUI()
    {
        UnitCommandsUI.gameObject.SetActive(false);
    }

    void HandleUnitCommandClick(int commandId)
    {
        DebugEx.DrawPoint(_unitCommandUIWorldPosition, 2f, Color.magenta, 10f);

        StartCoroutine(SendUnitCommandClick(commandId));
    }

    IEnumerator SendUnitCommandClick(int commandId)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        Entity[] yeah = _selected.ToArray();

        int i = 0;
        foreach (Entity selected in yeah)
        {
            yield return null;
            Entity entity = entityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(UnitCommandRequestRpc));
            GhostInstance ghostInstance = entityManager.GetComponentData<GhostInstance>(selected);
            entityManager.SetComponentData(entity, new UnitCommandRequestRpc()
            {
                Entity = ghostInstance,
                CommandId = commandId,

                WorldPosition = _unitCommandUIWorldPosition,
            });
            if (UnitCommandsUI.rootVisualElement != null)
            {
                float v = (float)(++i) / (float)yeah.Length;
                ProgressBar progressBar = UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress");
                progressBar.value = v;
                progressBar.style.display = DisplayStyle.Flex;
            }
        }
        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").style.display = DisplayStyle.None;
    }

    Entity[] UnitsInRect(Rect rect)
    {
        using EntityQuery selectablesQ = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(typeof(LocalToWorld), typeof(SelectableUnit));
        using NativeArray<Entity> selectableEntities = selectablesQ.ToEntityArray(Allocator.Temp);
        List<Entity> result = new(selectableEntities.Length);
        for (int i = 0; i < selectableEntities.Length; i++)
        {
            Entity selectableEntity = selectableEntities[i];
            LocalToWorld transform = ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<LocalToWorld>(selectableEntity);
            Vector3 point = MainCamera.Camera.WorldToScreenPoint(transform.Position);
            if (point.x < rect.xMin ||
                point.y < rect.yMin ||
                point.x > rect.xMax ||
                point.y > rect.yMax) continue;
            result.Add(selectableEntity);
        }
        return result.ToArray();
    }

    SelectableUnit GetUnitStatus(Entity unit) => ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<SelectableUnit>(unit);
    void SetUnitStatus(Entity unit, SelectableUnit status) => ConnectionManager.ClientOrDefaultWorld.EntityManager.SetComponentData<SelectableUnit>(unit, status);

    void SelectUnitCandidate(Entity unit, SelectionStatus status)
    {
        if (GetUnitStatus(unit).Status == SelectionStatus.None)
        {
            SetUnitStatus(unit, new()
            {
                Status = status,
            });
        }
        _candidates.Add(unit);
    }

    void DeselectUnitCandidate(Entity unit)
    {
        if (GetUnitStatus(unit).Status == SelectionStatus.Candidate)
        {
            SetUnitStatus(unit, new()
            {
                Status = SelectionStatus.None,
            });
        }
        _candidates.Remove(unit);
    }

    void ClearSelectionCandidates()
    {
        if (_candidates.Count == 0) return;
        foreach (Entity unit in _candidates)
        {
            if (GetUnitStatus(unit).Status != SelectionStatus.Candidate) continue;
            SetUnitStatus(unit, new()
            {
                Status = SelectionStatus.None,
            });
        }
        _candidates.Clear();
    }

    void SelectUnit(Entity unit, SelectionStatus status)
    {
        SetUnitStatus(unit, new()
        {
            Status = status,
        });
        _selected.Add(unit);
    }

    void DeselectUnit(Entity unit)
    {
        SetUnitStatus(unit, new()
        {
            Status = SelectionStatus.None,
        });
        _selected.Remove(unit);
    }

    void ClearSelection()
    {
        foreach (Entity unit in _selected)
        {
            SetUnitStatus(unit, new()
            {
                Status = SelectionStatus.None,
            });
        }
        _selected.Clear();
    }

    RaycastHit RayCast(UnityEngine.Ray ray, uint layer)
    {
        // FIXME: this is allocating memory every frame
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        CollisionWorld collisionWorld;
        using (EntityQuery singletonQuery = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(builder))
        {
            collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        }

        RaycastInput input = new()
        {
            Start = ray.origin,
            End = ray.GetPoint(200f),
            Filter = new CollisionFilter()
            {
                BelongsTo = Layers.All,
                CollidesWith = layer,
                GroupIndex = 0,
            },
        };

        if (collisionWorld.CastRay(input, out RaycastHit hit))
        { return hit; }

        return default;
    }
}
