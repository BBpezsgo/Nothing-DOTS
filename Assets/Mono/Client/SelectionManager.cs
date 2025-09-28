using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public class SelectionManager : Singleton<SelectionManager>
{
    const uint Layer = Layers.Selectable | Layers.BuildingPlaceholder;

    [SerializeField] float BoxSelectDistanceThreshold = default;
    [SerializeField, NotNull] RectTransform? SelectBox = default;
    [SerializeField, NotNull] UIDocument? UnitCommandsUI = default;
    [SerializeField, NotNull] VisualTreeAsset? UnitCommandItemUI = default;
    static readonly Plane Ground = new(Vector3.up, Vector3.zero);

    bool _isSelectBoxVisible;
    Vector3 _selectionStart = default;
    Vector3 _rightClick = default;
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
            _rightClick = Input.mousePosition;
            BeginUnitAction();
        }

        if (Input.GetMouseButtonUp(1))
        {
            if ((Input.mousePosition - _rightClick).sqrMagnitude > 10)
            {
                _firstHit = default;
            }
            else
            {
                FinishUnitAction();
            }
            _rightClick = default;
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

        //{
        //    UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        //    if (TerrainCollisionSystem.Raycast(ray.origin, ray.direction, 1000f, out float3 hitPoint))
        //    {
        //        DebugEx.DrawPoint(hitPoint, 1f, Color.white, 10f);
        //    }
        //}

        // {
        //     Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        //     if (Ground.Raycast(ray, out float distance))
        //     {
        //         var start = ray.origin;
        //         var end = ray.GetPoint(distance);
        //         // QuadrantSystem.DrawQuadrant(ray.GetPoint(distance));
        //         NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map = QuadrantSystem.GetMap(ConnectionManager.ClientOrDefaultWorld.Unmanaged);
        //         if (QuadrantRayCast.RayCast(map, start, end, Layers.All, out Hit hit))
        //         {
        //             DebugEx.DrawPoint(hit.Position, 2f, Color.cyan);
        //         }
        //     }
        // }
    }

    void BeginBoxSelect()
    {
        HideUnitCommandsUI();

        _selectionStart = default;

        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        if (!WorldRaycast(ray, out float distance))
        { return; }

        _selectionStart = ray.GetPoint(distance);
    }

    void UpdateBoxSelect()
    {
        ClearSelectionCandidates();

        if (_selectionStart == default)
        {
            SetSelectBoxVisible(false);

            if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
            Entity selectableHit = hit.Entity.Entity;
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
            if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
            Entity selectableHit = hit.Entity.Entity;
            SelectUnitCandidate(selectableHit, SelectionStatus.Candidate);
            SetSelectBoxVisible(false);
            return;
        }

        float minX = math.min(startPoint.x, endPoint.x);
        float minY = math.min(startPoint.y, endPoint.y);
        float maxX = math.max(startPoint.x, endPoint.x);
        float maxY = math.max(startPoint.y, endPoint.y);

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
            if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
            Entity selectableHit = hit.Entity.Entity;
            if (GetUnitStatus(selectableHit).Status == SelectionStatus.Selected &&
                _selected.Count > 0 &&
                Input.GetKey(KeyCode.LeftShift))
            { DeselectUnit(selectableHit); }
            else
            { SelectUnit(selectableHit, SelectionStatus.Selected); }
            return;
        }

        float minX = math.min(startPoint.x, endPoint.x);
        float minY = math.min(startPoint.y, endPoint.y);
        float maxX = math.max(startPoint.x, endPoint.x);
        float maxY = math.max(startPoint.y, endPoint.y);

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

        if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
        Entity selectableHit = hit.Entity.Entity;
        if (!IsMine(selectableHit)) return;

        _firstHit = selectableHit;
    }

    void FinishUnitAction()
    {
        if (_selected.Count > 0)
        {
            _firstHit = Entity.Null;
            ShowUnitCommandsUI();
            return;
        }

        Entity firstHit = _firstHit;
        _firstHit = Entity.Null;

        if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
        Entity selectableHit = hit.Entity.Entity;

        if (!IsMine(selectableHit)) return;

        if (selectableHit != firstHit) return;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (entityManager.HasComponent<Factory>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Factory)
                .Setup(FactoryManager.Instance, selectableHit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Facility>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Facility)
                .Setup(FacilityManager.Instance, selectableHit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Unit>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Builder>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<CoreComputer>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Transporter>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Extractor>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Pendrive>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.DiskDrive)
                .Setup(DiskDriveManager.Instance, selectableHit);
            return;
        }

        if (entityManager.HasComponent<Building>(selectableHit))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, selectableHit);
            return;
        }
    }

    void ShowUnitCommandsUI()
    {
        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        if (!WorldRaycast(ray, out float distance))
        { return; }

        _unitCommandUIWorldPosition = ray.GetPoint(distance);
        UnitCommandsUI.gameObject.SetActive(true);

        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").style.display = DisplayStyle.None;
        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").value = 0f;

        VisualElement container = UnitCommandsUI.rootVisualElement.Q("container-unit-commands");
        container.Clear();

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        foreach (Entity selected in _selected)
        {
            if (!entityManager.Exists(selected)) continue;
            if (!entityManager.HasBuffer<BufferedUnitCommandDefinition>(selected)) continue;

            DynamicBuffer<BufferedUnitCommandDefinition> commands = entityManager.GetBuffer<BufferedUnitCommandDefinition>(selected);

            for (int i = 0; i < commands.Length; i++)
            {
                string name = commands[i].Label.ToString();
                int id = commands[i].Id;
                bool added = false;

                foreach (VisualElement? existingItemUi in container.Children())
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

        if (container.childCount == 0) UnitCommandsUI.gameObject.SetActive(false);
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
        EntityArchetype commandRequestArchetype = entityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(SendRpcCommandRequest),
            typeof(UnitCommandRequestRpc),
        });

        // int i = 0;
        foreach (Entity selected in yeah)
        {
            yield return null;
            if (!entityManager.Exists(selected)) continue;
            Entity entity = entityManager.CreateEntity(commandRequestArchetype);
            GhostInstance ghostInstance = entityManager.GetComponentData<GhostInstance>(selected);
            entityManager.SetComponentData(entity, new UnitCommandRequestRpc()
            {
                Entity = ghostInstance,
                CommandId = commandId,

                WorldPosition = _unitCommandUIWorldPosition,
            });
            // if (UnitCommandsUI.rootVisualElement != null)
            // {
            //     float v = (float)(++i) / (float)yeah.Length;
            //     ProgressBar progressBar = UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress");
            //     progressBar.value = v;
            //     progressBar.style.display = DisplayStyle.Flex;
            // }
        }
        // if (UnitCommandsUI.rootVisualElement != null)
        // {
        //     UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").style.display = DisplayStyle.None;
        // }
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

    SelectableUnit GetUnitStatus(Entity unit)
    {
        if (!ConnectionManager.ClientOrDefaultWorld.EntityManager.Exists(unit) ||
            !ConnectionManager.ClientOrDefaultWorld.EntityManager.HasComponent<SelectableUnit>(unit))
        {
            return new SelectableUnit()
            {
                Status = SelectionStatus.None,
            };
        }

        return ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<SelectableUnit>(unit);
    }

    static bool IsMine(Entity unit)
    {
        if (!ConnectionManager.ClientOrDefaultWorld.EntityManager.Exists(unit)) return false;
        if (!ConnectionManager.ClientOrDefaultWorld.EntityManager.HasComponent<UnitTeam>(unit)) return true;
        UnitTeam unitTeam = ConnectionManager.ClientOrDefaultWorld.EntityManager.GetComponentData<UnitTeam>(unit);
        if (!PlayerManager.TryGetLocalPlayer(out Player localPlayer)) return false;
        return unitTeam.Team == localPlayer.Team;
    }

    void SetUnitStatus(Entity unit, SelectableUnit status)
    {
        if (!ConnectionManager.ClientOrDefaultWorld.EntityManager.Exists(unit) ||
            !ConnectionManager.ClientOrDefaultWorld.EntityManager.HasComponent<SelectableUnit>(unit)) return;
        ConnectionManager.ClientOrDefaultWorld.EntityManager.SetComponentData<SelectableUnit>(unit, status);
    }

    void SelectUnitCandidate(Entity unit, SelectionStatus status)
    {
        if (!IsMine(unit)) return;
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
        if (!IsMine(unit)) return;
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

    public static bool WorldRaycast(UnityEngine.Ray ray, out float distance)
    {
        return TerrainGenerator.Instance.Raycast(ray.origin, ray.direction, 1000f, out distance);
        //return Ground.Raycast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), out distance);
    }

    static bool RayCast(UnityEngine.Ray ray, uint layer, out Hit hit)
    {
        Vector3 start = ray.origin;
        Vector3 end = ray.GetPoint(200f);
        NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map = QuadrantSystem.GetMap(ConnectionManager.ClientOrDefaultWorld.Unmanaged);
        if (!QuadrantRayCast.RayCast(map, new Ray(start, end, layer), out hit)) return false;
        if (WorldRaycast(ray, out float worldHitDistance) && hit.Distance > worldHitDistance + 5f) return false;
        return true;
    }
}
