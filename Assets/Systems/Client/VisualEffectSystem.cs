using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class VisualEffectSystem : SystemBase
{
    ObjectPool<VisualEffectHandlerComponent>[]? Pools;

    protected override void OnCreate()
    {
        RequireForUpdate<VisualEffectDatabase>();
    }

    protected override void OnUpdate()
    {
        if (Pools == null)
        {
            DynamicBuffer<BufferedVisualEffect> database = SystemAPI.GetSingletonBuffer<BufferedVisualEffect>(true);
            Pools = new ObjectPool<VisualEffectHandlerComponent>[database.Length];
            for (int i = 0; i < Pools.Length; i++)
            {
                int _i = i;
                BufferedVisualEffect asset = database[_i];
                Pools[_i] = new ObjectPool<VisualEffectHandlerComponent>(
                    () =>
                    {
                        GameObject gameObject = new($"Effect {_i}");

                        VisualEffectHandlerComponent handlerComponent = gameObject.AddComponent<VisualEffectHandlerComponent>();
                        handlerComponent.Lifetime = asset.Duration;
                        handlerComponent.Asset = asset;
                        handlerComponent.Pool = Pools[_i];

                        VisualEffect visualEffect = handlerComponent.VisualEffect = gameObject.AddComponent<VisualEffect>();
                        visualEffect.visualEffectAsset = asset.VisualEffect.Value;

                        if (!asset.LightColor.Equals(default) && asset.LightRange > 0f && asset.LightIntensity > 0f)
                        {
                            Light light = handlerComponent.Light = gameObject.AddComponent<Light>();
                            light.color = new Color(asset.LightColor.x, asset.LightColor.y, asset.LightColor.z);
                            light.intensity = 0f;
                            light.range = asset.LightRange;
                            light.enabled = false;
                        }

                        return handlerComponent;
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

            Vector3 p = MainCamera.Camera.WorldToViewportPoint(spawn.ValueRO.Position);
            if (p.z < 0f || p.x < 0f || p.y < 0f || p.x > 1f || p.y > 1f) continue;
            if (math.distancesq(MainCamera.Camera.transform.position, spawn.ValueRO.Position) > 50f * 50f) continue;

            VisualEffectHandlerComponent effect = Pools[spawn.ValueRO.Index].Get();
            effect.transform.position = spawn.ValueRO.Position;
            if (effect.VisualEffect.HasVector3("direction")) effect.VisualEffect.SetVector3("direction", (spawn.ValueRO.Rotation.ToEuler() * Mathf.Rad2Deg) + new float3(90f, 0f, 0f));
            effect.VisualEffect.Play();
        }

        foreach (var (command, entity) in
            SystemAPI.Query<RefRO<VisualEffectRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            commandBuffer.DestroyEntity(entity);

            Vector3 p = MainCamera.Camera.WorldToViewportPoint(command.ValueRO.Position);
            if (p.z < 0f || p.x < 0f || p.y < 0f || p.x > 1f || p.y > 1f) continue;
            if (math.distancesq(MainCamera.Camera.transform.position, command.ValueRO.Position) > 50f * 50f) continue;

            VisualEffectHandlerComponent effect = Pools[command.ValueRO.Index].Get();
            effect.transform.position = command.ValueRO.Position;
            if (effect.VisualEffect.HasVector3("direction")) effect.VisualEffect.SetVector3("direction", (command.ValueRO.Rotation.ToEuler() * Mathf.Rad2Deg) + new float3(90f, 0f, 0f));
            effect.VisualEffect.Play();
        }
    }
}
