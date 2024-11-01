using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

public class SelectionManager : Singleton<SelectionManager>
{
    [SerializeField] float BoxSelectDistanceThreshold = default;
    [SerializeField, NotNull] RectTransform? SelectBox = default;

    Vector3 _selectionStart = default;
    HashSet<Entity> _selected = new();
    HashSet<Entity> _candidates = new();
    Entity _firstHit = Entity.Null;

    void Start()
    {
        _selected = new();
        _candidates = new();
    }

    void Update()
    {
        if (UI.IsMouseHandled)
        {
            _selectionStart = default;
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
    }

    void BeginBoxSelect()
    {
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
            SelectBox.gameObject.SetActive(false);

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
            SelectBox.gameObject.SetActive(false);
            return;
        }

        if (Vector2.Distance(startPoint, endPoint) < BoxSelectDistanceThreshold)
        {
            Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
            if (selectableHit == Entity.Null) return;
            SelectUnitCandidate(selectableHit, SelectionStatus.Candidate);
            SelectBox.gameObject.SetActive(false);
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
        SelectBox.gameObject.SetActive(true);
    }

    void FinishBoxSelect()
    {
        if (!Input.GetKey(KeyCode.LeftShift)) ClearSelection();
        SelectBox.gameObject.SetActive(false);

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
        if (_firstHit == Entity.Null) return;
        Entity firstHit = _firstHit;
        _firstHit = Entity.Null;

        Entity selectableHit = RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layers.Selectable).Entity;
        if (selectableHit == Entity.Null) return;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (selectableHit != firstHit) return;

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
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        CollisionWorld collisionWorld;
        using (EntityQuery singletonQuery = ConnectionManager.ClientOrDefaultWorld.EntityManager.CreateEntityQuery(builder))
        {
            collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        }

        RaycastInput input = new()
        {
            Start = ray.origin,
            End = ray.GetPoint(100f),
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
