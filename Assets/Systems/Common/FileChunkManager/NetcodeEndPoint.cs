using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct NetcodeEndPoint : IEquatable<NetcodeEndPoint>, IRpcCommandSerializer<NetcodeEndPoint>, IComponentData
{
    public NetworkId ConnectionId;
    public Entity ConnectionEntity;
    public readonly bool IsServer => ConnectionId.Value == default && ConnectionEntity == default;

    public static NetcodeEndPoint Server => new(default, default);

    public NetcodeEndPoint(NetworkId connectionId, Entity connectionEntity)
    {
        ConnectionId = connectionId;
        ConnectionEntity = connectionEntity;
    }

    public Entity GetEntity(EntityManager entityManager)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        using EntityQuery entityQ = entityManager.CreateEntityQuery(typeof(NetworkId));
        using NativeArray<Entity> entities = entityQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            NetworkId networkId = entityManager.GetComponentData<NetworkId>(entities[i]);
            if (networkId.Value != ConnectionId.Value) continue;
            return ConnectionEntity = entities[i];
        }
        throw new Exception($"Connection entity {ConnectionId} not found");
    }

    public Entity GetEntity(ref SystemState state)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        EntityQuery entityQ = state.GetEntityQuery(typeof(NetworkId));
        ComponentLookup<NetworkId> componentQ = state.GetComponentLookup<NetworkId>(true);
        return GetEntity(entityQ, componentQ);
    }
    public Entity GetEntity(in EntityQuery entityQ, in ComponentLookup<NetworkId> componentQ)
    {
        if (ConnectionEntity != Entity.Null) return ConnectionEntity;
        if (IsServer) return Entity.Null;
        Debug.Log(".");
        using NativeArray<Entity> entities = entityQ.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            RefRO<NetworkId> networkId = componentQ.GetRefRO(entities[i]);
            if (networkId.ValueRO.Value != ConnectionId.Value) continue;
            return ConnectionEntity = entities[i];
        }
        throw new Exception($"Connection entity {ConnectionId} not found");
    }

    public override readonly bool Equals(object obj) => obj is NetcodeEndPoint other && Equals(other);
    public readonly bool Equals(NetcodeEndPoint other) => ConnectionId.Value == other.ConnectionId.Value;
    public override readonly int GetHashCode() => ConnectionId.Value;
    public override readonly string ToString() => IsServer ? "SERVER" : ConnectionId.ToString();

    public readonly void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in NetcodeEndPoint data)
    {
        writer.WriteInt(data.ConnectionId.Value);
    }

    public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref NetcodeEndPoint data)
    {
        data.ConnectionId = new NetworkId()
        {
            Value = reader.ReadInt()
        };
    }

    static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer = new(InvokeExecute);
    public readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute() => InvokeExecuteFunctionPointer;
    [BurstCompile(DisableDirectCall = true)]
    static void InvokeExecute(ref RpcExecutor.Parameters parameters) => RpcExecutor.ExecuteCreateRequestComponent<NetcodeEndPoint, NetcodeEndPoint>(ref parameters);

    public static bool operator ==(NetcodeEndPoint a, NetcodeEndPoint b) => a.ConnectionId.Value == b.ConnectionId.Value;
    public static bool operator !=(NetcodeEndPoint a, NetcodeEndPoint b) => a.ConnectionId.Value != b.ConnectionId.Value;
}
