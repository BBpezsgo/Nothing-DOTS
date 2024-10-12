using System;
using System.Collections.Generic;
using NaughtyAttributes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

#nullable enable

public class BuildingManager : MonoBehaviour
{
    static BuildingManager instance;

    [SerializeField, ReadOnly] GameObject? SelectedBuilding;
    [SerializeField] GameObject BuildingHologramPrefab;
    [SerializeField, ReadOnly] GameObject BuildingHologram;

    [SerializeField] public Material HologramMaterial;

    [SerializeField, ReadOnly] bool IsValidPosition = false;

    [SerializeField] Color ValidHologramColor = Color.white;
    [SerializeField] Color InvalidHologramColor = Color.red;
    [SerializeField, Range(-10f, 10f)] float HologramEmission = 1.1f;

    public bool IsBuilding => SelectedBuilding != null;

    [Header("Buildings")]
    [SerializeField] GameObject[] Buildings;

    [Header("UI")]
    [SerializeField] VisualTreeAsset BuildingButton;
    [SerializeField] UIDocument BuildingUI;

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
        SelectedBuilding = null;
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

        for (int i = 0; i < Buildings.Length; i++)
        {
            TemplateContainer newElement = BuildingButton.Instantiate();

            Button button = newElement.Q<Button>();
            button.name = $"btn-{i}";
            button.clickable.clickedWithEventInfo += Clickable_clickedWithEventInfo;

            newElement.Q<VisualElement>("image").style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            button.text = $"{Buildings[i].name}";

            container.Add(newElement);
        }
    }

    void PlaceBuilding(Vector3 position, GameObject building)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery buildingDatabaseQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(BuildingDatabase) });
        if (!buildingDatabaseQuery.TryGetSingletonEntity<BuildingDatabase>(out Entity buildingDatabase))
        {
            Debug.LogWarning($"Failed to get {nameof(buildingDatabase)} entity singleton");
            return;
        }

        DynamicBuffer<BufferedEntityPrefab> entityPrefabs = entityManager.GetBuffer<BufferedEntityPrefab>(buildingDatabase, true);

        int buildingIndex = Buildings.IndexOf(building);
        if (buildingIndex == -1)
        {
            Debug.LogWarning($"Building index not found");
            return;
        }

        Entity newEntity = entityManager.Instantiate(entityPrefabs[buildingIndex].Entity);
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
        GameObject building = Buildings[i];
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
        SelectedBuilding = null;

        BuildingUI.gameObject.SetActive(false);

        if (BuildingHologram != null)
        { BuildingHologram.SetActive(false); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            SelectedBuilding = null;
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

        if (SelectedBuilding == null)
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
            // material.SetEmissionColor(IsValidPosition ? ValidHologramColor : InvalidHologramColor, HologramEmission);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SelectedBuilding = null;
            BuildingHologram.SetActive(false);
            IsValidPosition = false;
            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            if (SelectedBuilding == null) return;
            if (!IsValidPosition) return;

            PlaceBuilding(position, SelectedBuilding);
            Hide();
            return;
        }
    }

    static void ApplyHologram(GameObject hologram, GameObject buildingPrefab)
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
