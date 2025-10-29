using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct RadarProcessorSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, radar, transform) in
            SystemAPI.Query<RefRW<Processor>, RefRW<Radar>, RefRO<LocalTransform>>())
        {
            ref MappedMemory mapped = ref processor.ValueRW.Memory.MappedMemory;

            if (!float.IsFinite(mapped.Radar.RadarDirection)) continue;

            quaternion target = quaternion.EulerXYZ(
                0f,
                -mapped.Radar.RadarDirection + math.PIHALF,
                0f);
            RefRW<LocalTransform> radarTransform = SystemAPI.GetComponentRW<LocalTransform>(radar.ValueRO.Transform);

            // const float speed = 720f;
            // Utils.RotateTowards(ref radarTransform.ValueRW.Rotation, target, speed * SystemAPI.Time.DeltaTime);
            radarTransform.ValueRW.Rotation = target;

            if (!processor.ValueRO.RadarResponse.Equals(float.NaN))
            {
                DebugEx.DrawPoint(transform.ValueRO.TransformPoint(processor.ValueRO.RadarResponse), 1f, Color.magenta, 3f, false);
            }

            mapped.Radar.RadarResponse = processor.ValueRO.RadarResponse;
        }
    }
}
