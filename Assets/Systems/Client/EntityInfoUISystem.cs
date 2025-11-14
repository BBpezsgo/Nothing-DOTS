using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class EntityInfoUISystem : SystemBase
{
#if UNITY_EDITOR && ENABLE_PROFILER
    static readonly Unity.Profiling.ProfilerMarker __instantiateUI = new($"{nameof(EntityInfoUISystem)}.InstantiateUI");
    static readonly Unity.Profiling.ProfilerMarker __destroyUI = new($"{nameof(EntityInfoUISystem)}.DestroyUI");
#endif

    Transform? _canvas;

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        if (_canvas == null)
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.name != "UICanvas") continue;
                _canvas = canvas.transform;
                break;
            }
        }

        foreach (var (transform, selectable, entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<SelectableUnit>>()
            .WithNone<EntityInfoUIReference>()
            .WithAll<EntityWithInfoUI>()
            .WithEntityAccess())
        {
            if (selectable.ValueRO.Status == SelectionStatus.None) continue;

#if UNITY_EDITOR && ENABLE_PROFILER
            using Unity.Profiling.ProfilerMarker.AutoScope _ = __instantiateUI.Auto();
#endif

            GameObject uiPrefab = SystemAPI.ManagedAPI.GetSingleton<UIPrefabs>().EntityInfo;
            float3 spawnPosition = transform.ValueRO.Position;
            GameObject newUi = Object.Instantiate(uiPrefab, spawnPosition, Quaternion.identity, _canvas);
            var comp = newUi.GetComponent<EntityInfoUI>();

            if (SystemAPI.HasComponent<MeshBounds>(entity))
            {
                comp.Bounds = SystemAPI.GetComponent<MeshBounds>(entity).Bounds;
            }

            commandBuffer.AddComponent<EntityInfoUIReference>(entity, new()
            {
                Value = comp,
            });

            break;
        }

        foreach (var (uiRef, damageable) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<Damageable>>())
        {
            uiRef.Value.HealthPercent = damageable.ValueRO.Health / damageable.ValueRO.MaxHealth;
        }

        foreach (var (uiRef, buildingPlaceholder) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<BuildingPlaceholder>>())
        {
            uiRef.Value.BuildingProgressPercent = buildingPlaceholder.ValueRO.CurrentProgress / buildingPlaceholder.ValueRO.TotalProgress;
        }

        foreach (var (uiRef, transporter) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<Transporter>>())
        {
            uiRef.Value.TransporterLoadPercent = (float)transporter.ValueRO.CurrentLoad / (float)Transporter.Capacity;
            uiRef.Value.TransporterProgressPercent = transporter.ValueRO.LoadProgress;
        }

        foreach (var (uiRef, extractor) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<Extractor>>())
        {
            uiRef.Value.ExtractorProgressPercent = extractor.ValueRO.ExtractProgress;
        }

        foreach (var (uiRef, transform) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<LocalTransform>>())
        {
            uiRef.Value.Position = transform.ValueRO.Position;
            uiRef.Value.Rotation = transform.ValueRO.Rotation;
        }

        foreach (var (uiRef, unitTeam, selectable) in
            SystemAPI.Query<EntityInfoUIReference, RefRO<UnitTeam>, RefRO<SelectableUnit>>())
        {
            uiRef.Value.SelectionStatus = selectable.ValueRO.Status;
            uiRef.Value.Team = unitTeam.ValueRO.Team;
            uiRef.Value.gameObject.SetActive(selectable.ValueRO.Status != SelectionStatus.None);
        }

        foreach (var (uiRef, entity) in
            SystemAPI.Query<EntityInfoUIReference>()
            .WithNone<EntityWithInfoUI>()
            .WithEntityAccess())
        {
#if UNITY_EDITOR && ENABLE_PROFILER
            using Unity.Profiling.ProfilerMarker.AutoScope _ = __destroyUI.Auto();
#endif

            Object.Destroy(uiRef.Value.gameObject);
            commandBuffer.RemoveComponent<EntityInfoUIReference>(entity);
        }
    }
}
