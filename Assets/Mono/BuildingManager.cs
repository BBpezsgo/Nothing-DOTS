using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ReadOnlyAttribute = NaughtyAttributes.ReadOnlyAttribute;

#nullable enable

public struct PlaceBuildingRequestRpcCommand : IRpcCommand
{
    public float3 Position;
    public FixedString32Bytes BuildingName;
}

public class BuildingManager : MonoBehaviour
{
    static BuildingManager instance;

    BufferedBuilding SelectedBuilding;
    [SerializeField, NotNull] GameObject? BuildingHologramPrefab;
    [SerializeField, ReadOnly, NotNull] GameObject? BuildingHologram;

    [SerializeField, NotNull] Material? HologramMaterial;

    [SerializeField, ReadOnly] bool IsValidPosition = false;

    [SerializeField] Color ValidHologramColor = Color.white;
    [SerializeField] Color InvalidHologramColor = Color.red;
    [SerializeField, Range(-10f, 10f)] float HologramEmission = 1.1f;

    public bool IsBuilding => SelectedBuilding.Prefab != default;

    [Header("UI")]
    [SerializeField, NotNull] VisualTreeAsset? BuildingButton;
    [SerializeField, NotNull] UIDocument? BuildingUI;

    void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning($"[{nameof(BuildingManager)}]: Instance already registered, destroying self");
            GameObject.Destroy(this);
            return;
        }
        instance = this;
    }

    void OnKeyEsc()
    {
        SelectedBuilding = default;
        if (BuildingHologram != null)
        { BuildingHologram.SetActive(false); }

        if (BuildingUI.gameObject.activeSelf)
        {
            IsValidPosition = false;
            Hide();
        }
    }

    void ListBuildings()
    {
        VisualElement container = BuildingUI.rootVisualElement.Q<VisualElement>("unity-content-container");
        container.Clear();

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery buildingDatabaseQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(BuildingDatabase) });
        if (!buildingDatabaseQuery.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(BuildingDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedBuilding> buildings = entityManager.GetBuffer<BufferedBuilding>(buildingDatabase, true);

        for (int i = 0; i < buildings.Length; i++)
        {
            TemplateContainer newElement = BuildingButton.Instantiate();

            Button button = newElement.Q<Button>();
            button.name = $"btn-{i}";
            button.clickable.clickedWithEventInfo += Clickable_clickedWithEventInfo;

            newElement.Q<VisualElement>("image").style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            button.text = $"{buildings[i].Name}";

            container.Add(newElement);
        }
    }

    void PlaceBuilding(Vector3 position, BufferedBuilding building)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        Entity newEntity = entityManager.Instantiate(building.Prefab);
        entityManager.SetComponentData(newEntity, new LocalTransform
        {
            Position = position,
            Rotation = quaternion.identity,
            Scale = 1f,
        });
    }

    void Clickable_clickedWithEventInfo(EventBase e)
    {
        if (e.target is not Button button) return;
        int i = int.Parse(button.name.Split('-')[1]);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery buildingDatabaseQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(BuildingDatabase) });
        if (!buildingDatabaseQuery.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(BuildingDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedBuilding> buildings = entityManager.GetBuffer<BufferedBuilding>(buildingDatabase, true);

        BufferedBuilding building = buildings[i];
        SelectedBuilding = building;
        if (BuildingHologram != null)
        { ApplyHologram(BuildingHologram, SelectedBuilding); }
    }

    void Show()
    {
        BuildingUI.gameObject.SetActive(true);
        ListBuildings();
    }

    void Hide()
    {
        SelectedBuilding = default;

        BuildingUI.gameObject.SetActive(false);

        if (BuildingHologram != null)
        { BuildingHologram.SetActive(false); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            SelectedBuilding = default;
            if (BuildingHologram != null)
            { BuildingHologram.SetActive(false); }

            if (BuildingUI.gameObject.activeSelf)
            {
                IsValidPosition = false;
                Hide();
                return;
            }
            else
            {
                IsValidPosition = false;
                Show();
            }
        }

        if (Mouse.current.rightButton.isPressed && (IsBuilding || BuildingUI.gameObject.activeSelf))
        {
            Hide();
            return;
        }

        if (SelectedBuilding.Prefab == default)
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

        Vector3 position = Camera.main.ScreenToWorldPosition(Mouse.current.position.value);
        position.y = 0f;

        if (Input.GetKey(KeyCode.LeftControl))
        { position = new Vector3(MathF.Round(position.x), position.y, MathF.Round(position.z)); }

        BuildingHologram.transform.position = position;

        // Vector3 checkPosition = position;

        // Debug3D.DrawBox(checkPosition, SelectedBuilding.SpaceNeed, Color.white, Time.deltaTime);

        // if (Physics.OverlapBox(checkPosition, SelectedBuilding.SpaceNeed / 2, Quaternion.identity, LayerMask.GetMask(LayerMaskNames.Default, LayerMaskNames.Water)).Length > 0)
        // { IsValidPosition = false; }
        // else
        { IsValidPosition = true; }

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

        if (Mouse.current.leftButton.isPressed)
        {
            if (SelectedBuilding.Prefab == default) return;
            if (!IsValidPosition) return;

            if (World.DefaultGameObjectInjectionWorld.IsServer())
            {
                PlaceBuilding(position, SelectedBuilding);
            }
            else
            {
                SendPlaceBuildingRequest(new PlaceBuildingRequestRpcCommand()
                {
                    BuildingName = SelectedBuilding.Name,
                    Position = position,
                }, ConnectionManager.ClientWorld);
            }

            Hide();
            return;
        }
    }

    void SendPlaceBuildingRequest(PlaceBuildingRequestRpcCommand request, World? world)
    {
        if (world == null || !world.IsCreated) return;
        Entity entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(PlaceBuildingRequestRpcCommand));
        world.EntityManager.SetComponentData(entity, request);
    }

    static void ApplyHologram(GameObject hologram, BufferedBuilding buildingPrefab)
    {
        GameObject hologramModels = GetHologramModelGroup(hologram);
        hologramModels.transform.SetPositionAndRotation(default, Quaternion.identity);
        // CopyModel(buildingPrefab, hologramModels);

        List<MeshRenderer> renderers = new();
        renderers.AddRange(hologram.GetComponentsInChildren<MeshRenderer>());

        for (int i = 0; i < renderers.Count; i++)
        { renderers[i].materials = new Material[] { Material.Instantiate(instance.HologramMaterial) }; }
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

    static void CopyModel(GameObject from, GameObject to)
    {
        /*
        MeshRenderer[] meshRenderers = from.GetComponentsInChildren<MeshRenderer>(false);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            MeshRenderer meshRenderer = meshRenderers[i];
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            Vector3 relativePosition = from.transform.InverseTransformPoint(meshRenderer.transform.position);
            Vector3 relativeRotation = from.transform.InverseTransformDirection(meshRenderer.transform.eulerAngles);

            GameObject newObject = new(meshRenderer.gameObject.name);
            newObject.transform.SetParent(to.transform);
            newObject.transform.SetLocalPositionAndRotation(relativePosition, Quaternion.Euler(relativeRotation));
            newObject.transform.localScale = meshRenderer.transform.localScale;

            MeshRenderer.Instantiate(meshRenderer, newObject.transform);
            MeshFilter.Instantiate(meshFilter, newObject.transform);
        }
        */

        to.transform.localScale = from.transform.localScale;
        to.transform.SetLocalPositionAndRotation(from.transform.localPosition, from.transform.localRotation);

        if (from.TryGetComponent<MeshRenderer>(out MeshRenderer? meshRenderer) &&
            !to.TryGetComponent<MeshRenderer>(out _))
        {
            MeshRenderer.Instantiate(meshRenderer, to.transform);
            MeshFilter.Instantiate(from.GetComponent<MeshFilter>(), to.transform);
        }

        int childCount = from.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            GameObject prefabChild = from.transform.GetChild(i).gameObject;
            GameObject newHologramChild = new(prefabChild.name);
            newHologramChild.transform.SetParent(to.transform);
            CopyModel(prefabChild, newHologramChild);
        }
    }
}
