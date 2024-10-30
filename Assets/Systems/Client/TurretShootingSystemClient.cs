using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct TurretShootingSystemClient : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        DynamicBuffer<BufferedProjectile> projectiles = SystemAPI.GetSingletonBuffer<BufferedProjectile>(true);
        EntityCommandBuffer commandBuffer = new(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ShootRpc>>()
            .WithEntityAccess())
        {
            Entity projectilePrefab = projectiles[command.ValueRO.ProjectileIndex].Prefab;

            Entity instance = commandBuffer.Instantiate(projectilePrefab);
            commandBuffer.SetComponent(instance, new LocalTransform
            {
                Position = command.ValueRO.Position,
                Rotation = quaternion.identity,
                Scale = SystemAPI.GetComponent<LocalTransform>(projectilePrefab).Scale
            });
            commandBuffer.SetComponent(instance, new Projectile
            {
                Velocity = command.ValueRO.Velocity
            });
            commandBuffer.DestroyEntity(entity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
