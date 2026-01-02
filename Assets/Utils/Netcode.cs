using Unity.Entities;
using Unity.NetCode;

public static class NetcodeUtils
{
    public static bool IsLocal(this WorldUnmanaged world) => !world.IsServer() && !world.IsClient();

    public static Entity CreateRPC<T>(WorldUnmanaged world)
        where T : unmanaged, IComponentData
    {
        return CreateRPCImpl(world, ComponentType.ReadOnly<T>(), Entity.Null);
    }

    public static Entity CreateRPC<T>(WorldUnmanaged world, Entity connectionEntity)
        where T : unmanaged, IComponentData
    {
        if (connectionEntity == Entity.Null) return Entity.Null;
        return CreateRPCImpl(world, ComponentType.ReadOnly<T>(), connectionEntity);
    }

    public static Entity CreateRPC<T>(WorldUnmanaged world, T componentData)
        where T : unmanaged, IComponentData
    {
        Entity entity = CreateRPCImpl(in world, ComponentType.ReadWrite<T>(), Entity.Null);
        world.EntityManager.SetComponentData(entity, componentData);
        return entity;
    }

    public static Entity CreateRPC<T>(WorldUnmanaged world, T componentData, Entity connectionEntity)
        where T : unmanaged, IComponentData
    {
        if (connectionEntity == Entity.Null) return Entity.Null;
        Entity entity = CreateRPCImpl(in world, ComponentType.ReadWrite<T>(), connectionEntity);
        world.EntityManager.SetComponentData(entity, componentData);
        return entity;
    }

    static Entity CreateRPCImpl(in WorldUnmanaged world, ComponentType componentType, Entity connectionEntity = default)
    {
        if (world.IsClient() || world.IsServer())
        {
            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadWrite<SendRpcCommandRequest>(),
            });

            world.EntityManager.SetComponentData(entity, new SendRpcCommandRequest() { TargetConnection = connectionEntity });
            return entity;
        }
        else
        {
            Entity entity = world.EntityManager.CreateEntity(stackalloc ComponentType[]
            {
                componentType,
                ComponentType.ReadOnly<SendRpcCommandRequest>(),
                ComponentType.ReadWrite<ReceiveRpcCommandRequest>(),
            });

            world.EntityManager.SetComponentData(entity, new ReceiveRpcCommandRequest() { SourceConnection = connectionEntity });
            return entity;
        }
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world)
        where T : unmanaged, IComponentData
    {
        return CreateRPCImpl(commandBuffer, in world, ComponentType.ReadOnly<T>(), Entity.Null);
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, Entity connectionEntity)
        where T : unmanaged, IComponentData
    {
        if (connectionEntity == Entity.Null) return Entity.Null;
        return CreateRPCImpl(commandBuffer, in world, ComponentType.ReadOnly<T>(), connectionEntity);
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, T componentData)
        where T : unmanaged, IComponentData
    {
        Entity entity = CreateRPCImpl(commandBuffer, in world, ComponentType.ReadWrite<T>(), Entity.Null);
        commandBuffer.SetComponent(entity, componentData);
        return entity;
    }

    public static Entity CreateRPC<T>(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, T componentData, Entity connectionEntity)
        where T : unmanaged, IComponentData
    {
        if (connectionEntity == Entity.Null) return Entity.Null;
        Entity entity = CreateRPCImpl(commandBuffer, in world, ComponentType.ReadWrite<T>(), connectionEntity);
        commandBuffer.SetComponent(entity, componentData);
        return entity;
    }

    static Entity CreateRPCImpl(EntityCommandBuffer commandBuffer, in WorldUnmanaged world, ComponentType componentType, Entity connectionEntity = default)
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
}
