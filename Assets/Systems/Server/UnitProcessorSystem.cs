using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using u8 = System.Byte;
using i8 = System.SByte;
using u16 = System.UInt16;
using i16 = System.Int16;
using u32 = System.UInt32;
using i32 = System.Int32;
using f32 = System.Single;

#nullable enable

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateAfter(typeof(ProcessorSystem))]
partial struct UnitProcessorSystem : ISystem
{
    ComponentLookup<Turret> _turretLookup;
    ComponentLookup<LocalTransform> _transformLookup;

    void ISystem.OnCreate(ref SystemState state)
    {
        _turretLookup = state.GetComponentLookup<Turret>(false);
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MappedMemory
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
    }

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        _turretLookup.Update(ref state);
        _transformLookup.Update(ref state);

        foreach ((RefRW<Processor> processor, RefRW<Unit> unit, RefRW<LocalToWorld> transform, Entity entity) in
                    SystemAPI.Query<RefRW<Processor>, RefRW<Unit>, RefRW<LocalToWorld>>()
                    .WithEntityAccess())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + (Processor.HeapSize + Processor.StackSize));

            unit.ValueRW.Input = new float2(
                mapped->InputSteer / 128f,
                mapped->InputForward / 128f
            );

            mapped->Position = new(transform.ValueRO.Position.x, transform.ValueRO.Position.z);
            mapped->Forward = new(transform.ValueRO.Forward.x, transform.ValueRO.Forward.z);

            if (state.EntityManager.HasBuffer<Child>(entity))
            {
                foreach (Child child in state.EntityManager.GetBuffer<Child>(entity))
                {
                    RefRW<Turret> turret = _turretLookup.GetRefRWOptional(child.Value);
                    if (!turret.IsValid) continue;
                    RefRO<LocalTransform> turretTransform = _transformLookup.GetRefROOptional(child.Value);
                    if (!turretTransform.IsValid) continue;

                    if (mapped->InputShoot != 0)
                    {
                        turret.ValueRW.ShootRequested = true;
                        mapped->InputShoot = 0;
                    }

                    turret.ValueRW.TargetRotation = mapped->TurretTargetRotation;
                    turret.ValueRW.TargetAngle = mapped->TurretTargetAngle;

                    Utils.QuaternionToEuler(turretTransform.ValueRO.Rotation, out float3 euler);
                    mapped->TurretCurrentRotation = euler.y;
                    mapped->TurretCurrentAngle = euler.x;

                    break;
                }
            }
        }
    }
}
