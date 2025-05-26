using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using System;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct CombatTurretProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, combatTurret) in
            SystemAPI.Query<RefRW<Processor>, RefRW<CombatTurret>>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            if (mapped->CombatTurret.InputShoot != 0)
            {
                combatTurret.ValueRW.ShootRequested = true;
                mapped->CombatTurret.InputShoot = 0;
            }

            RefRW<LocalTransform> turretTransform = SystemAPI.GetComponentRW<LocalTransform>(combatTurret.ValueRO.Turret);
            RefRW<LocalTransform> cannonTransform = SystemAPI.GetComponentRW<LocalTransform>(combatTurret.ValueRO.Cannon);

            turretTransform.ValueRO.Rotation.ToEuler(out float3 turretEuler);
            cannonTransform.ValueRO.Rotation.ToEuler(out float3 cannonEuler);

            mapped->CombatTurret.TurretCurrentRotation = turretEuler.y;
            mapped->CombatTurret.TurretCurrentAngle = cannonEuler.x;

            if (float.IsFinite(mapped->CombatTurret.TurretTargetRotation))
            {
                float y = Utils.MoveTowardsAngle(turretEuler.y, mapped->CombatTurret.TurretTargetRotation, combatTurret.ValueRO.TurretRotationSpeed * SystemAPI.Time.DeltaTime);
                turretTransform.ValueRW.Rotation = quaternion.EulerXYZ(0, y, 0);
            }

            if (float.IsFinite(mapped->CombatTurret.TurretTargetAngle))
            {
                float x = Utils.MoveTowardsAngle(cannonEuler.x, Math.Clamp(mapped->CombatTurret.TurretTargetAngle, combatTurret.ValueRO.MinAngle, combatTurret.ValueRO.MaxAngle), combatTurret.ValueRO.CannonRotationSpeed * SystemAPI.Time.DeltaTime);
                cannonTransform.ValueRW.Rotation = quaternion.EulerXYZ(x, 0, 0);
            }
        }
    }
}
