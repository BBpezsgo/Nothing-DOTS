using Unity.Entities;
using Unity.NetCode;

public static class NetcodeUtils
{
    public static bool IsLocal(this WorldUnmanaged world) => !world.IsServer() && !world.IsClient();

    public static Entity CreateRPC(ref SystemState state, ComponentType componentType, Entity connectionEntity = default)
        => CreateRPC(state.EntityManager, state.WorldUnmanaged, componentType, connectionEntity);

    public static Entity CreateRPC<T>(ref SystemState state, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(state.EntityManager, state.WorldUnmanaged, connectionEntity);

    public static Entity CreateRPC<T>(ref SystemState state, T componentData, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(state.EntityManager, state.WorldUnmanaged, componentData, connectionEntity);

    public static Entity CreateRPC(SystemBase state, ComponentType componentType, Entity connectionEntity = default)
        => CreateRPC(state.EntityManager, state.World.Unmanaged, componentType, connectionEntity);

    public static Entity CreateRPC<T>(SystemBase state, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(state.EntityManager, state.World.Unmanaged, connectionEntity);

    public static Entity CreateRPC<T>(SystemBase state, T componentData, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(state.EntityManager, state.World.Unmanaged, componentData, connectionEntity);

    public static Entity CreateRPC(WorldUnmanaged world, ComponentType componentType, Entity connectionEntity = default)
        => CreateRPC(world.EntityManager, world, componentType, connectionEntity);

    public static Entity CreateRPC<T>(WorldUnmanaged world, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(world.EntityManager, world, connectionEntity);

    public static Entity CreateRPC<T>(WorldUnmanaged world, T componentData, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
        => CreateRPC<T>(world.EntityManager, world, componentData, connectionEntity);

    public static Entity CreateRPC(in EntityManager entityManager, in WorldUnmanaged world, ComponentType componentType, Entity connectionEntity = default)
    {
        if (world.IsClient() || world.IsServer())
        {
            Entity entity = entityManager.CreateEntity(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadWrite<SendRpcCommandRequest>(),
            });

            entityManager.SetComponentData(entity, new SendRpcCommandRequest() { TargetConnection = connectionEntity });
            return entity;
        }
        else
        {
            Entity entity = entityManager.CreateEntity(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadOnly<SendRpcCommandRequest>(),
                ComponentType.ReadWrite<ReceiveRpcCommandRequest>(),
            });

            entityManager.SetComponentData(entity, new ReceiveRpcCommandRequest() { SourceConnection = connectionEntity });
            return entity;
        }
    }

    public static Entity CreateRPC<T>(in EntityManager entityManager, in WorldUnmanaged world, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
    {
        return CreateRPC(in entityManager, in world, ComponentType.ReadOnly<T>(), connectionEntity);
    }

    public static Entity CreateRPC<T>(in EntityManager entityManager, in WorldUnmanaged world, T componentData, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
    {
        Entity entity = CreateRPC(in entityManager, in world, ComponentType.ReadWrite<T>(), connectionEntity);
        entityManager.SetComponentData(entity, componentData);
        return entity;
    }

    public static Entity CreateRPC(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, ComponentType componentType, Entity connectionEntity = default)
    {
        if (world.Time.ElapsedTime < 1d)
        {
            if (world.IsClient() || world.IsServer())
            {
                Entity _entity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(_entity, componentType);
                commandBuffer.AddComponent<SendRpcCommandRequest>(_entity, new() { TargetConnection = connectionEntity });
                return _entity;
            }
            else
            {
                Entity _entity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(_entity, componentType);
                commandBuffer.AddComponent<SendRpcCommandRequest>(_entity, new() { TargetConnection = connectionEntity });
                commandBuffer.AddComponent<ReceiveRpcCommandRequest>(_entity, new() { SourceConnection = connectionEntity });
                return _entity;
            }
        }

        if (world.IsClient() || world.IsServer())
        {
            Entity entity = commandBuffer.CreateEntity(world.EntityManager.CreateArchetype(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadWrite<SendRpcCommandRequest>(),
            }));

            commandBuffer.SetComponent(entity, new SendRpcCommandRequest() { TargetConnection = connectionEntity });
            return entity;
        }
        else
        {
            Entity entity = commandBuffer.CreateEntity(world.EntityManager.CreateArchetype(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadOnly<SendRpcCommandRequest>(),
                ComponentType.ReadWrite<ReceiveRpcCommandRequest>(),
            }));

            commandBuffer.SetComponent(entity, new ReceiveRpcCommandRequest() { SourceConnection = connectionEntity });
            return entity;
        }
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
    {
        return CreateRPC(commandBuffer, in world, ComponentType.ReadOnly<T>(), connectionEntity);
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, T componentData, Entity connectionEntity = default)
        where T : unmanaged, IComponentData
    {
        Entity entity = CreateRPC(commandBuffer, in world, ComponentType.ReadWrite<T>(), connectionEntity);
        commandBuffer.SetComponent(entity, componentData);
        return entity;
    }
}
