using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct EntityInfoUISystem : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (transform, _, entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<EntityWithInfoUI>>()
            .WithNone<EntityInfoUIReference>()
            .WithEntityAccess())
        {
            GameObject uiPrefab = SystemAPI.ManagedAPI.GetSingleton<UIPrefabs>().EntityInfo;
            Unity.Mathematics.float3 spawnPosition = transform.ValueRO.Position;
            GameObject newUi = Object.Instantiate(uiPrefab, spawnPosition, Quaternion.identity, Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Exclude).transform);

            entityCommandBuffer.AddComponent<EntityInfoUIReference>(entity, new()
            {
                Value = newUi.GetComponent<EntityInfoUI>(),
            });
        }

        foreach (var (transform, damageable, uiRef, selectable) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<Damageable>, EntityInfoUIReference, RefRO<SelectableUnit>>())
        {
            uiRef.Value.WorldPosition = transform.ValueRO.Position;
            uiRef.Value.Percent = damageable.ValueRO.Health / damageable.ValueRO.MaxHealth;
            uiRef.Value.SelectionStatus = selectable.ValueRO.Status;
            uiRef.Value.gameObject.SetActive(selectable.ValueRO.Status != SelectionStatus.None);
        }

        foreach (var (uiRef, entity) in
            SystemAPI.Query<EntityInfoUIReference>()
            .WithNone<EntityWithInfoUI>()
            .WithEntityAccess())
        {
            Object.Destroy(uiRef.Value.gameObject);
            entityCommandBuffer.RemoveComponent<EntityInfoUIReference>(entity);
        }
    }
}
