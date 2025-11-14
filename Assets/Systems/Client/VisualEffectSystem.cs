using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class VisualEffectSystem : SystemBase
{
    ObjectPool<VisualEffect>[]? Pools;

    protected override void OnCreate()
    {
        RequireForUpdate<VisualEffectDatabase>();
    }

    protected override void OnUpdate()
    {
        if (Pools == null)
        {
            DynamicBuffer<BufferedVisualEffect> database = SystemAPI.GetSingletonBuffer<BufferedVisualEffect>(true);
            Pools = new ObjectPool<VisualEffect>[database.Length];
            for (int i = 0; i < Pools.Length; i++)
            {
                int _i = i;
                VisualEffectAsset asset = database[_i].VisualEffect.Value;
                Pools[_i] = new ObjectPool<VisualEffect>(
                    () =>
                    {
                        GameObject gameObject = new($"Effect {_i}");

                        VisualEffect visualEffect = gameObject.AddComponent<VisualEffect>();
                        visualEffect.visualEffectAsset = asset;

                        VisualEffectHandlerComponent handlerComponent = gameObject.AddComponent<VisualEffectHandlerComponent>();
                        handlerComponent.Lifetime = 0.5f;
                        handlerComponent.Pool = Pools[_i];
                        handlerComponent.VisualEffect = visualEffect;

                        return visualEffect;
                    },
                    (v) =>
                    {
                        v.gameObject.SetActive(true);
                        v.GetComponent<VisualEffectHandlerComponent>().Reinit();
                        v.Reinit();
                    },
                    (v) =>
                    {
                        v.gameObject.SetActive(false);
                    });
            }
        }

        EntityCommandBuffer commandBuffer = default;

        foreach (var (spawn, entity) in
            SystemAPI.Query<RefRO<VisualEffectSpawn>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            var p = MainCamera.Camera.WorldToViewportPoint(spawn.ValueRO.Position);
            if (p.z < 0f || p.x < 0f || p.y < 0f || p.x > 1f || p.y > 1f) continue;
            if (math.distancesq(MainCamera.Camera.transform.position, spawn.ValueRO.Position) > 50f * 50f) continue;

            VisualEffect effect = Pools[spawn.ValueRO.Index].Get();
            effect.transform.position = spawn.ValueRO.Position;
            if (effect.HasVector3("direction")) effect.SetVector3("direction", (spawn.ValueRO.Rotation.ToEuler() * Mathf.Rad2Deg) + new float3(90f, 0f, 0f));
            effect.Play();
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<VisualEffectRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            var p = MainCamera.Camera.WorldToViewportPoint(command.ValueRO.Position);
            if (p.z < 0f || p.x < 0f || p.y < 0f || p.x > 1f || p.y > 1f) continue;
            if (math.distancesq(MainCamera.Camera.transform.position, command.ValueRO.Position) > 50f * 50f) continue;

            VisualEffect effect = Pools[command.ValueRO.Index].Get();
            effect.transform.position = command.ValueRO.Position;
            if (effect.HasVector3("direction")) effect.SetVector3("direction", (command.ValueRO.Rotation.ToEuler() * Mathf.Rad2Deg) + new float3(90f, 0f, 0f));
            effect.Play();
        }
    }
}
