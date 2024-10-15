using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using u8 = System.Byte;
using i8 = System.SByte;
using u16 = System.UInt16;
using i16 = System.Int16;
using u32 = System.UInt32;
using i32 = System.Int32;
using f32 = System.Single;

[UpdateAfter(typeof(ProcessorSystem))]
partial struct ProcessorAPISystem : ISystem
{
    ComponentLookup<Turret> _turretLookup;
    ComponentLookup<LocalTransform> _transformLookup;

    void ISystem.OnCreate(ref SystemState state)
    {
        _turretLookup = state.GetComponentLookup<Turret>(false);
        _transformLookup = state.GetComponentLookup<LocalTransform>(false);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MappedMemory
    {
        public i8 InputForward;
        public i8 InputSteer;
        public u8 InputShoot;
        public f32 TurretTargetRotation;
        public f32 TurretTargetAngle;
        public f32 TurretCurrentRotation;
        public f32 TurretCurrentAngle;
        public f32 PositionX;
        public f32 PositionY;
        public f32 ForwardX;
        public f32 ForwardY;
    }

    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        _turretLookup.Update(ref state);
        _transformLookup.Update(ref state);

        foreach ((Processor processor, RefRW<Unit> unit, RefRW<LocalToWorld> transform, Entity entity) in
                    SystemAPI.Query<Processor, RefRW<Unit>, RefRW<LocalToWorld>>()
                    .WithEntityAccess())
        {
            System.Span<byte> memory = processor.MappedMemory;
            if (memory.IsEmpty) continue;

            fixed (byte* _mapped = memory)
            {
                MappedMemory* mapped = (MappedMemory*)_mapped;

                unit.ValueRW.Input = new float2(
                    mapped->InputForward / 128f,
                    mapped->InputSteer / 128f
                );

                mapped->PositionX = transform.ValueRO.Position.x;
                mapped->PositionY = transform.ValueRO.Position.z;
                mapped->ForwardX = transform.ValueRO.Forward.x;
                mapped->ForwardY = transform.ValueRO.Forward.z;

                if (state.EntityManager.HasBuffer<Child>(entity))
                {
                    foreach (Child child in state.EntityManager.GetBuffer<Child>(entity))
                    {
                        RefRW<Turret> turret = _turretLookup.GetRefRWOptional(child.Value);
                        if (!turret.IsValid) continue;
                        RefRW<LocalTransform> turretTransform = _transformLookup.GetRefRWOptional(child.Value);
                        if (!turretTransform.IsValid) continue;

                        if (mapped->InputShoot != 0)
                        {
                            turret.ValueRW.ShootRequested = true;
                            mapped->InputShoot = 0;
                        }

                        turret.ValueRW.TargetRotation = mapped->TurretTargetRotation;
                        turret.ValueRW.TargetAngle = mapped->TurretTargetAngle;

                        float3 euler = math.EulerXYZ(turretTransform.ValueRO.Rotation);
                        mapped->TurretCurrentRotation = euler.y;
                        mapped->TurretCurrentAngle = euler.x;

                        break;
                    }
                }

                if (false)
                    Debug.Log(
                        $"{{\n" +
                        $"  InputForward: {mapped->InputForward};\n" +
                        $"  InputSteer: {mapped->InputSteer};\n" +
                        $"  InputShoot: {mapped->InputShoot};\n" +
                        $"  TurretTargetRotation: {mapped->TurretTargetRotation};\n" +
                        $"  TurretTargetAngle: {mapped->TurretTargetAngle};\n" +
                        $"  TurretCurrentRotation: {mapped->TurretCurrentRotation};\n" +
                        $"  TurretCurrentAngle: {mapped->TurretCurrentAngle};\n" +
                        $"  PositionX: {mapped->PositionX};\n" +
                        $"  PositionY: {mapped->PositionY};\n" +
                        $"  ForwardX: {mapped->ForwardX};\n" +
                        $"  ForwardY: {mapped->ForwardY};\n" +
                        $"}}");
            }
        }
    }
}
