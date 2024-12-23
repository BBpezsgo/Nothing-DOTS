using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct RadarProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, radar) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Radar>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);
            if (!float.IsFinite(mapped->Radar.RadarDirection)) continue;

            RefRW<LocalTransform> radarTransform = SystemAPI.GetComponentRW<LocalTransform>(radar.ValueRO.Transform);
            // const float speed = 720f;
            quaternion target = quaternion.EulerXYZ(
                0f,
                -mapped->Radar.RadarDirection + math.PIHALF,
                0f);
            // Utils.RotateTowards(ref radarTransform.ValueRW.Rotation, target, speed * SystemAPI.Time.DeltaTime);
            radarTransform.ValueRW.Rotation = target;
            mapped->Radar.RadarResponse = processor.ValueRW.RadarResponse;
        }
    }
}
