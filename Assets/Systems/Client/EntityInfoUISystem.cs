using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
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
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                _canvas = canvas.transform;
                break;
            }
        }

        foreach (var (transform, entity) in
            SystemAPI.Query<RefRO<LocalTransform>>()
            .WithNone<EntityInfoUIReference>()
            .WithAll<EntityWithInfoUI>()
            .WithEntityAccess())
        {
#if UNITY_EDITOR && ENABLE_PROFILER
            using Unity.Profiling.ProfilerMarker.AutoScope _ = __instantiateUI.Auto();
#endif

            GameObject uiPrefab = SystemAPI.ManagedAPI.GetSingleton<UIPrefabs>().EntityInfo;
            Unity.Mathematics.float3 spawnPosition = transform.ValueRO.Position;
            GameObject newUi = Object.Instantiate(uiPrefab, spawnPosition, Quaternion.identity, _canvas);

            commandBuffer.AddComponent<EntityInfoUIReference>(entity, new()
            {
                Value = newUi.GetComponent<EntityInfoUI>(),
            });
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
            uiRef.Value.WorldPosition = transform.ValueRO.Position;
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
