using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ReadOnlyAttribute = NaughtyAttributes.ReadOnlyAttribute;

public class BuildingManager : PrivateSingleton<BuildingManager>, IUISetup, IUICleanup
{
    BufferedBuilding SelectedBuilding = default;
    [SerializeField, NotNull] GameObject? BuildingHologramPrefab = default;
    [SerializeField, ReadOnly, NotNull] GameObject? BuildingHologram = default;

    [SerializeField, NotNull] Material? HologramMaterial = default;

    [SerializeField, ReadOnly] bool IsValidPosition = false;

    [SerializeField] Color ValidHologramColor = Color.white;
    [SerializeField] Color InvalidHologramColor = Color.red;
    [SerializeField, Range(-10f, 10f)] float HologramEmission = 1.1f;

    public bool IsBuilding => SelectedBuilding.Prefab != default;

    [Header("UI")]

    [SerializeField, NotNull] VisualTreeAsset? BuildingButton = default;
    [SerializeField, NotNull] UIDocument? BuildingUI = default;

    float refreshAt = default;
    float refreshedBySyncAt = default;
    float syncAt = default;

    void OnKeyEsc()
    {
        SelectedBuilding = default;
        if (BuildingHologram != null)
        { BuildingHologram.SetActive(false); }

        if (BuildingUI.gameObject.activeSelf)
        {
            IsValidPosition = false;
            UIManager.Instance.CloseUI(this);
        }
    }

    void RefreshUI()
    {
        VisualElement container = BuildingUI.rootVisualElement.Q<VisualElement>("unity-content-container");
        container.Clear();

        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;
        using EntityQuery buildingDatabaseQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(BuildingDatabase) });
        if (!buildingDatabaseQuery.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(BuildingDatabase)} entity singleton");
            return;
        }

        container.SyncList(BuildingsSystemClient.GetInstance(entityManager.WorldUnmanaged).Buildings, BuildingButton, (item, element, recycled) =>
        {
            element.userData = item.Name;

            Button button = element.Q<Button>();
            if (!recycled) button.clicked += () =>
            {
                SelectBuilding((Unity.Collections.FixedString32Bytes)element.userData);
                button.Blur();
            };

            element.Q<Label>("label-name").text = item.Name.ToString();
            element.Q<Label>("label-resources").text = item.RequiredResources.ToString();
        });
    }

    void SelectBuilding(Unity.Collections.FixedString32Bytes buildingName)
    {
        EntityManager entityManager = ConnectionManager.ClientOrDefaultWorld.EntityManager;

        using EntityQuery buildingDatabaseQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(BuildingDatabase) });
        if (!buildingDatabaseQuery.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(BuildingDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedBuilding> buildings = entityManager.GetBuffer<BufferedBuilding>(buildingDatabase, true);

        BufferedBuilding building = default;

        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Name != buildingName) continue;
            building = buildings[i];
            break;
        }

        if (building.Prefab == Entity.Null)
        {
            Debug.LogWarning($"Building \"{buildingName}\" not found in the database");
            return;
        }

        SelectedBuilding = building;
        if (BuildingHologram != null)
        { ApplyHologram(BuildingHologram, SelectedBuilding); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B) && (!UI.IsUIFocused || BuildingUI.gameObject.activeSelf))
        {
            SelectedBuilding = default;
            if (BuildingHologram != null)
            { BuildingHologram.SetActive(false); }

            if (BuildingUI.gameObject.activeSelf)
            {
                UIManager.Instance.CloseUI(this);
                return;
            }
            else if (!UIManager.Instance.AnyUIVisible)
            {
                UIManager.Instance.OpenUI(this.BuildingUI)
                    .Setup(this);
            }
        }

        if (!BuildingUI.gameObject.activeSelf) return;

        if (UIManager.Instance.GrapESC())
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame &&
            !UI.IsMouseHandled &&
            (IsBuilding || BuildingUI.gameObject.activeSelf) &&
            !CameraControl.Instance.IsDragging)
        {
            if (SelectedBuilding.Prefab != Entity.Null)
            {
                SelectedBuilding = default;
                if (BuildingHologram != null)
                { BuildingHologram.SetActive(false); }
            }
            else
            {
                UIManager.Instance.CloseUI(this);
            }
            return;
        }

        if (BuildingUI == null || !BuildingUI.gameObject.activeSelf) return;

        if (Time.time >= refreshAt ||
            refreshedBySyncAt != BuildingsSystemClient.LastSynced.Data)
        {
            refreshedBySyncAt = BuildingsSystemClient.LastSynced.Data;
            RefreshUI();
            refreshAt = Time.time + 1f;
        }

        if (Time.time >= syncAt)
        {
            syncAt = Time.time + 5f;
            BuildingsSystemClient.Refresh(ConnectionManager.ClientOrDefaultWorld.Unmanaged);
        }

        if (SelectedBuilding.Prefab == Entity.Null)
        {
            if (BuildingHologram != null)
            { BuildingHologram.SetActive(false); }
            return;
        }

        if (BuildingHologram == null)
        {
            BuildingHologram = GameObject.Instantiate(BuildingHologramPrefab, transform);
            ApplyHologram(BuildingHologram, SelectedBuilding);
        }
        else if (!BuildingHologram.activeSelf)
        {
            BuildingHologram.SetActive(true);
            ApplyHologram(BuildingHologram, SelectedBuilding);
        }

        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Mouse.current.position.value);
        Plane plane = new(Vector3.up, Vector3.zero);

        if (!plane.Raycast(ray, out float distance))
        { return; }

        Vector3 position = ray.GetPoint(distance);
        position.y = 0f;

        if (Input.GetKey(KeyCode.LeftControl))
        { position = new Vector3(math.round(position.x), position.y, math.round(position.z)); }

        BuildingHologram.transform.position = position;

        var map = QuadrantSystem.GetMap(ConnectionManager.ClientOrDefaultWorld.Unmanaged);
        Collider placeholderCollider = new AABBCollider(true, new AABB() { Extents = new float3(1f, 1f, 1f) });

        IsValidPosition = !CollisionSystem.Intersect(
            map,
            placeholderCollider, position,
            out _, out _);

        MeshRenderer[] renderers = BuildingHologram.GetComponentsInChildren<MeshRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].material;
            material.color = IsValidPosition ? ValidHologramColor : InvalidHologramColor;
            material.SetEmissionColor(IsValidPosition ? ValidHologramColor : InvalidHologramColor, HologramEmission);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SelectedBuilding = default;
            BuildingHologram.SetActive(false);
            IsValidPosition = false;
            return;
        }

        if (Mouse.current.leftButton.isPressed && !UI.IsMouseHandled)
        {
            if (SelectedBuilding.Prefab == default) return;
            if (!IsValidPosition)
            {
                Debug.Log($"Invalid building position");
                return;
            }

            if (ConnectionManager.ClientOrDefaultWorld.IsServer())
            {
                throw new NotImplementedException();
            }
            else
            {
                SendPlaceBuildingRequest(new PlaceBuildingRequestRpc()
                {
                    BuildingName = SelectedBuilding.Name,
                    Position = position,
                }, ConnectionManager.ClientOrDefaultWorld);
            }

            UIManager.Instance.CloseUI(this);
            return;
        }
    }

    void SendPlaceBuildingRequest(PlaceBuildingRequestRpc request, World world)
    {
        Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
        {
            typeof(SendRpcCommandRequest),
            typeof(PlaceBuildingRequestRpc),
        });
        world.EntityManager.SetComponentData(entity, request);
    }

    static void ApplyHologram(GameObject hologram, BufferedBuilding buildingPrefab)
    {
        GameObject hologramModels = GetHologramModelGroup(hologram);
        hologramModels.transform.SetPositionAndRotation(default, Quaternion.identity);

        List<MeshRenderer> renderers = new();
        renderers.AddRange(hologram.GetComponentsInChildren<MeshRenderer>());

        for (int i = 0; i < renderers.Count; i++)
        { renderers[i].materials = new Material[] { Material.Instantiate(Instance.HologramMaterial) }; }
    }

    static GameObject GetHologramModelGroup(GameObject hologram)
    {
        Transform hologramModels = hologram.transform.Find("Model");
        if (hologramModels != null)
        { Destroy(hologramModels.gameObject); }

        hologramModels = new GameObject("Model").transform;
        hologramModels.SetParent(hologram.transform);
        hologramModels.localPosition = default;
        return hologramModels.gameObject;
    }

    public void Setup(UIDocument ui)
    {
        ui.gameObject.SetActive(true);
        RefreshUI();
        syncAt = 0f;
    }

    public void Cleanup(UIDocument ui)
    {
        SelectedBuilding = default;
        ui.gameObject.SetActive(false);
        if (BuildingHologram != null)
        { BuildingHologram.SetActive(false); }
    }
}
