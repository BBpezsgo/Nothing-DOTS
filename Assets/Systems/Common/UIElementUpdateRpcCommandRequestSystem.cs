using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.NetCode;

[UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
[CreateAfter(typeof(RpcSystem))]
[BurstCompile]
partial struct UIElementUpdateRpcCommandRequestSystem : ISystem
{
    RpcCommandRequest<UIElementUpdateRpc, UIElementUpdateRpc> _request;

    [BurstCompile]
    struct SendRpc : IJobChunk
    {
        public RpcCommandRequest<UIElementUpdateRpc, UIElementUpdateRpc>.SendRpcData data;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            data.Execute(chunk, unfilteredChunkIndex);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        _request.OnCreate(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SendRpc sendJob = new()
        {
            data = _request.InitJobData(ref state)
        };
        state.Dependency = sendJob.Schedule(_request.Query, state.Dependency);
    }
}
