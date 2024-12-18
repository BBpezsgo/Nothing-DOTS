using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

// [UpdateAfter(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct EntityInfoUISystem : ISystem
{
#if UNITY_EDITOR && ENABLE_PROFILER
    static readonly Unity.Profiling.ProfilerMarker __instantiateUI = new($"{nameof(EntityInfoUISystem)}.InstantiateUI");
    static readonly Unity.Profiling.ProfilerMarker __destroyUI = new($"{nameof(EntityInfoUISystem)}.DestroyUI");
#endif

    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (transform, _, entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityWithInfoUI>>()
            .WithNone<EntityInfoUIReference>()
            .WithEntityAccess())
        {
            using Unity.Profiling.ProfilerMarker.AutoScope _ = __instantiateUI.Auto();

            GameObject uiPrefab = SystemAPI.ManagedAPI.GetSingleton<UIPrefabs>().EntityInfo;
            Unity.Mathematics.float3 spawnPosition = transform.ValueRO.Position;
            GameObject newUi = Object.Instantiate(uiPrefab, spawnPosition, Quaternion.identity, Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Exclude).transform);

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
            using Unity.Profiling.ProfilerMarker.AutoScope _ = __destroyUI.Auto();

            Object.Destroy(uiRef.Value.gameObject);
            commandBuffer.RemoveComponent<EntityInfoUIReference>(entity);
        }
    }
}
