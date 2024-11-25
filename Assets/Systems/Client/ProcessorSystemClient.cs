using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct ProcessorSystemClient : ISystem
{
    ComponentLookup<URPMaterialPropertyEmissionColor> _emissionColorQ;

    [BurstCompile]
    void ISystem.OnCreate(ref SystemState state)
    {
        _emissionColorQ = state.GetComponentLookup<URPMaterialPropertyEmissionColor>();
    }

    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        _emissionColorQ.Update(ref state);
        float now = MonoTime.Now;

        foreach (RefRO<Processor> processor in
            SystemAPI.Query<RefRO<Processor>>())
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
                    processor.ValueRO.NetworkSendLED.IsOn(now) ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;

            if (processor.ValueRO.NetworkReceiveLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.NetworkReceiveLED.LED).ValueRW.Value =
                    processor.ValueRO.NetworkReceiveLED.IsOn(now) ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;

            if (processor.ValueRO.RadarLED.LED != Entity.Null)
                _emissionColorQ.GetRefRW(processor.ValueRO.RadarLED.LED).ValueRW.Value =
                    processor.ValueRO.RadarLED.IsOn(now) ?
                    new float4(0.1f, 0.2f, 1f, 1f) * 10f :
                    default;
        }
    }
}
