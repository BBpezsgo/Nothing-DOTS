using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

using u8 = System.Byte;
using i8 = System.SByte;
using u16 = System.UInt16;
using i16 = System.Int16;
using u32 = System.UInt32;
using i32 = System.Int32;
using f32 = System.Single;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct BuilderProcessorSystem : ISystem
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MappedMemory
    {
        public i8 InputForward;
        public i8 InputSteer;
        public u8 InputShoot;
        public f32 TurretTargetRotation;
        public f32 TurretTargetAngle;
        public f32 TurretCurrentRotation;
        public f32 TurretCurrentAngle;
        public float2 Position;
        public float2 Forward;
        public f32 RadarDirection;
        public f32 RadarResponse;
    }

    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, builder, vehicle, transform, entity) in
                    SystemAPI.Query<RefRW<Processor>, RefRW<Builder>, RefRW<Vehicle>, RefRW<LocalToWorld>>()
                    .WithEntityAccess())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            vehicle.ValueRW.Input = new float2(
                mapped->InputSteer / 128f,
                mapped->InputForward / 128f
            );

            mapped->Position = new(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
            mapped->Forward = new(transform.ValueRO.Forward.x, transform.ValueRO.Forward.z);

            if (builder.ValueRO.Turret != Entity.Null)
            {
                RefRW<Turret> turret = SystemAPI.GetComponentRW<Turret>(builder.ValueRO.Turret);
                RefRO<LocalTransform> turretTransform = SystemAPI.GetComponentRO<LocalTransform>(builder.ValueRO.Turret);

                if (mapped->InputShoot != 0)
                {
                    turret.ValueRW.ShootRequested = true;
                    mapped->InputShoot = 0;
                }

                if (float.IsFinite(mapped->TurretTargetRotation))
                {
                    turret.ValueRW.TargetRotation = mapped->TurretTargetRotation;
                }

                if (float.IsFinite(mapped->TurretTargetAngle))
                {
                    turret.ValueRW.TargetAngle = mapped->TurretTargetAngle + math.PIHALF;
                }

                Utils.QuaternionToEuler(turretTransform.ValueRO.Rotation, out float3 euler);
                mapped->TurretCurrentRotation = euler.y;
                mapped->TurretCurrentAngle = euler.x - math.PIHALF;
            }
        }
    }
}