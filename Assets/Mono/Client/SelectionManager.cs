using System;
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

class VirtualGhostEntity : IEquatable<VirtualGhostEntity>
{
    public readonly Entity Entity;
    public readonly GhostInstance GhostInstance;
    public readonly UnitCommandDefinition[] UnitCommands;

    public VirtualGhostEntity(Entity entity, GhostInstance ghostInstance, UnitCommandDefinition[] unitCommands)
    {
        Entity = entity;
        GhostInstance = ghostInstance;
        UnitCommands = unitCommands;
    }

    public override bool Equals(object? obj) => obj is VirtualGhostEntity other && Equals(other);
    public bool Equals(VirtualGhostEntity other) => GhostInstance.ghostId == other.GhostInstance.ghostId && GhostInstance.spawnTick == other.GhostInstance.spawnTick;
    public override int GetHashCode() => HashCode.Combine(GhostInstance.ghostId, GhostInstance.spawnTick);

    public static bool operator ==(VirtualGhostEntity left, VirtualGhostEntity right) => left.Equals(right);
    public static bool operator !=(VirtualGhostEntity left, VirtualGhostEntity right) => !left.Equals(right);
}

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
    HashSet<VirtualGhostEntity> _selected = new();
    HashSet<Entity> _candidates = new();
    Entity _firstHit = Entity.Null;
    Vector3 _unitCommandUIWorldPositionData = default;
    Vector3 _unitCommandUIPosition = default;

    public bool IsUnitCommandsActive => UnitCommandsUI.isActiveAndEnabled;

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
        if (UnitCommandsUI.isActiveAndEnabled)
        {
            if (UIManager.Instance.GrapESC())
            {
                HideUnitCommandsUI();
            }
            else
            {
                DebugEx.DrawPoint(_unitCommandUIPosition, 2f, Color.white);

                Vector3 screenPoint = MainCamera.Camera.WorldToScreenPoint(_unitCommandUIPosition);
                if (screenPoint.z >= 0f)
                {
                    screenPoint.z = 0f;
                    screenPoint.y = MainCamera.Camera.pixelHeight - screenPoint.y;
                    UnitCommandsUI.rootVisualElement.transform.position = screenPoint;
                }
            }
        }

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

        foreach (var item in _candidates)
        {
            SetUnitStatus(item, new SelectableUnit() { Status = SelectionStatus.Candidate });
        }

        foreach (var item in _selected)
        {
            SetUnitStatus(item.Entity, new SelectableUnit() { Status = SelectionStatus.Selected });
        }
    }

    void BeginBoxSelect()
    {
        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        _selectionStart = WorldRaycast(ray, out float distance) ? ray.GetPoint(distance) : ray.GetPoint(300f);
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

        HideUnitCommandsUI();

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

    static VirtualGhostEntity CreateVirtualGhostEntity(EntityManager entityManager, Entity entity)
    {
        UnitCommandDefinition[] commands = TryGetUnitCommands(entityManager, entity, out ReadOnlySpan<UnitCommandDefinition> buffer)
            ? buffer.ToArray()
            : Array.Empty<UnitCommandDefinition>();
        return new VirtualGhostEntity(
            entity,
            entityManager.GetComponentData<GhostInstance>(entity),
            commands
        );
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

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (Vector2.Distance(startPoint, endPoint) < BoxSelectDistanceThreshold)
        {
            if (!RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit)) return;
            VirtualGhostEntity virtualGhostEntity = CreateVirtualGhostEntity(entityManager, hit.Entity.Entity);
            if (GetUnitStatus(virtualGhostEntity.Entity).Status == SelectionStatus.Selected &&
                _selected.Count > 0 &&
                Input.GetKey(KeyCode.LeftShift))
            { DeselectUnit(virtualGhostEntity); }
            else
            { SelectUnit(virtualGhostEntity, SelectionStatus.Selected); }
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
        {
            VirtualGhostEntity virtualGhostEntity = CreateVirtualGhostEntity(entityManager, unit);
            SelectUnit(virtualGhostEntity, SelectionStatus.Selected);
        }
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
        Entity firstHit = _firstHit;
        _firstHit = Entity.Null;

        if (RayCast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), Layer, out Hit hit) && hit.Entity.Entity == firstHit)
        {
            if (OpenEntityUI(hit.Entity.Entity))
            {
                return;
            }
        }

        if (_selected.Count > 0)
        {
            ShowUnitCommandsUI();
            return;
        }
    }

    bool OpenEntityUI(Entity entity)
    {
        if (!IsMine(entity)) return false;

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        if (entityManager.HasComponent<Factory>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Factory)
                .Setup(FactoryManager.Instance, entity)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Facility>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Facility)
                .Setup(FacilityManager.Instance, entity)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Unit>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Builder>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<CoreComputer>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Transporter>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Extractor>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Pendrive>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.DiskDrive)
                .Setup(DiskDriveManager.Instance, entity);
            return true;
        }

        if (entityManager.HasComponent<Building>(entity))
        {
            UIManager.Instance.OpenUI(UIManager.Instance.Unit)
                .Setup(TerminalManager.Instance, entity);
            return true;
        }

        return false;
    }

    static bool TryGetUnitCommands(EntityManager entityManager, Entity selected, out ReadOnlySpan<UnitCommandDefinition> commands)
    {
        commands = default;

        if (!entityManager.Exists(selected)) return false;
        if (!entityManager.HasComponent<Processor>(selected)) return false;
        SerializableDictionary<FileId, CompiledSource> compiledSources = ConnectionManager.ClientOrDefaultWorld.GetExistingSystemManaged<CompilerSystemClient>().CompiledSources;
        if (!compiledSources.TryGetValue(entityManager.GetComponentData<Processor>(selected).SourceFile, out CompiledSource? source)) return false;

        commands = source.UnitCommandDefinitions.HasValue ? source.UnitCommandDefinitions.Value.AsReadOnlySpan() : ReadOnlySpan<UnitCommandDefinition>.Empty;
        return true;
    }

    void ShowUnitCommandsUI()
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Input.mousePosition);
        _unitCommandUIWorldPositionData = WorldRaycast(ray, out float distance) ? ray.GetPoint(distance) : default;
        _unitCommandUIPosition = _unitCommandUIWorldPositionData == default ? ray.GetPoint(300) : _unitCommandUIWorldPositionData;

        UnitCommandsUI.gameObject.SetActive(true);

        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").style.display = DisplayStyle.None;
        UnitCommandsUI.rootVisualElement.Q<ProgressBar>("progress").value = 0f;

        VisualElement container = UnitCommandsUI.rootVisualElement.Q("container-unit-commands");
        container.Clear();

        foreach (VirtualGhostEntity selected in _selected)
        {
            if (!TryGetUnitCommands(entityManager, selected.Entity, out var commands)) continue;

            for (int i = 0; i < commands.Length; i++)
            {
                UnitCommandDefinition command = commands[i];

                if (_unitCommandUIWorldPositionData == default && command.GetParameters().ToArray().Any(v => v is UnitCommandParameter.Position2 or UnitCommandParameter.Position3))
                {
                    // Position not avaliable, skipping
                    continue;
                }

                VisualElement? added = container.Children().FirstOrDefault(v =>
                {
                    (UnitCommandDefinition, int) d = ((UnitCommandDefinition, int))v.userData;
                    return d.Item1.Id == command.Id && d.Item1.Label == command.Label;
                });

                string name = command.Label.ToString();
                int id = command.Id;

                VisualElement itemUi = added ?? UnitCommandItemUI.Instantiate();
                itemUi.Q<Button>("unit-command-name").text = $"#{id} {name}{(added is null ? null : $" ({(((UnitCommandDefinition, int))added.userData).Item2 + 1})")}";
                itemUi.Q<Button>("unit-command-name").clicked += () => HandleUnitCommandClick(id);
                itemUi.userData = (command, added is null ? 1 : (((UnitCommandDefinition, int))added.userData).Item2 + 1);

                if (added is null) container.Add(itemUi);
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
        DebugEx.DrawPoint(_unitCommandUIWorldPositionData, 2f, Color.magenta, 10f);

        StartCoroutine(SendUnitCommandClick(commandId));
    }

    IEnumerator SendUnitCommandClick(int commandId)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        VirtualGhostEntity[] yeah = _selected.ToArray();
        EntityArchetype commandRequestArchetype = entityManager.CreateArchetype(stackalloc ComponentType[]
        {
            typeof(SendRpcCommandRequest),
            typeof(UnitCommandRequestRpc),
        });

        // int i = 0;
        foreach (VirtualGhostEntity selected in yeah)
        {
            yield return null;
            Entity entity = entityManager.CreateEntity(commandRequestArchetype);
            entityManager.SetComponentData(entity, new UnitCommandRequestRpc()
            {
                Entity = selected.GhostInstance,
                CommandId = commandId,

                WorldPosition = _unitCommandUIWorldPositionData,
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
                point.y > rect.yMax)
            { continue; }
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
            !ConnectionManager.ClientOrDefaultWorld.EntityManager.HasComponent<SelectableUnit>(unit))
        { return; }
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

    void SelectUnit(VirtualGhostEntity unit, SelectionStatus status)
    {
        if (!IsMine(unit.Entity)) return;
        SetUnitStatus(unit.Entity, new()
        {
            Status = status,
        });
        _selected.Add(unit);
        HUDManager.Instance._labelSelectedUnits.text = _selected.Count.ToString();
    }

    void DeselectUnit(VirtualGhostEntity unit)
    {
        SetUnitStatus(unit.Entity, new()
        {
            Status = SelectionStatus.None,
        });
        _selected.Remove(unit);
        HUDManager.Instance._labelSelectedUnits.text = _selected.Count.ToString();
    }

    void ClearSelection()
    {
        foreach (VirtualGhostEntity unit in _selected)
        {
            SetUnitStatus(unit.Entity, new()
            {
                Status = SelectionStatus.None,
            });
        }
        _selected.Clear();
        HUDManager.Instance._labelSelectedUnits.text = _selected.Count.ToString();
    }

    public static bool WorldRaycast(UnityEngine.Ray ray, out float distance)
    {
        return TerrainGenerator.Instance.Raycast(ray.origin, ray.direction, 300f, out distance, out _);
        //return Ground.Raycast(MainCamera.Camera.ScreenPointToRay(Input.mousePosition), out distance);
    }

    static bool RayCast(UnityEngine.Ray ray, uint layer, out Hit hit)
    {
        Vector3 start = ray.origin;
        Vector3 end = ray.GetPoint(300f);
        NativeParallelHashMap<uint, NativeList<QuadrantEntity>>.ReadOnly map = QuadrantSystem.GetMap(ConnectionManager.ClientOrDefaultWorld.Unmanaged);
        if (!QuadrantRayCast.RayCast(map, new Ray(start, end, layer), out hit)) return false;
        if (WorldRaycast(ray, out float worldHitDistance) && hit.Distance > worldHitDistance + 5f) return false;
        return true;
    }
}
