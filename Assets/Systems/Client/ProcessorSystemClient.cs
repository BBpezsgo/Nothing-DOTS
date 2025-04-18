using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct ProcessorSystemClient : ISystem
{
    ComponentLookup<URPMaterialPropertyEmissionColor> _emissionColorQ;
    public NativeList<UserUIElement> uiElements;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        _emissionColorQ = state.GetComponentLookup<URPMaterialPropertyEmissionColor>();
        uiElements = new(256, Allocator.Persistent);
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = default;

        _emissionColorQ.Update(ref state);
        float now = MonoTime.Now;

        foreach (RefRW<Processor> processor in
            SystemAPI.Query<RefRW<Processor>>())
        {
            if (processor.ValueRO.StatusLED.LED != Entity.Null)
            {
                _emissionColorQ.GetRefRW(processor.ValueRO.StatusLED.LED).ValueRW.Value = processor.ValueRO.StatusLED.Status switch
                {
                    1 => new float4(0f, 1f, 0f, 1f) * 10f,
                    2 => new float4(1f, 1f, 0f, 1f) * 10f,
                    _ => default,
                };
            }

            if (processor.ValueRO.NetworkSendLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.NetworkSendLED.LED).ValueRW.Value =
                    processor.ValueRW.NetworkSendLED.ReceiveBlink() ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;

            if (processor.ValueRO.NetworkReceiveLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.NetworkReceiveLED.LED).ValueRW.Value =
                    processor.ValueRW.NetworkReceiveLED.ReceiveBlink() ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;

            if (processor.ValueRO.RadarLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.RadarLED.LED).ValueRW.Value =
                    processor.ValueRW.RadarLED.ReceiveBlink() ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;

            if (processor.ValueRO.USBLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.USBLED.LED).ValueRW.Value =
                    processor.ValueRW.USBLED.ReceiveBlink() ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<UIElementUpdateRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            for (int i = 0; i < uiElements.Length; i++)
            {
                if (uiElements[i].Id != command.ValueRO.UIElement.Id) continue;
                uiElements[i] = command.ValueRO.UIElement;
                goto next;
            }

            uiElements.Add(command.ValueRO.UIElement);

        next:;
        }

        foreach (var (request, command, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<UIElementDestroyRpc>>()
            .WithEntityAccess())
        {
            if (!commandBuffer.IsCreated) commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            commandBuffer.DestroyEntity(entity);

            for (int i = 0; i < uiElements.Length; i++)
            {
                if (uiElements[i].Id != command.ValueRO.Id) continue;
                uiElements.RemoveAt(i);
                break;
            }
        }
    }

    public static ref ProcessorSystemClient GetInstance(in WorldUnmanaged world)
    {
        SystemHandle handle = world.GetExistingUnmanagedSystem<ProcessorSystemClient>();
        return ref world.GetUnsafeSystemRef<ProcessorSystemClient>(handle);
    }
}
