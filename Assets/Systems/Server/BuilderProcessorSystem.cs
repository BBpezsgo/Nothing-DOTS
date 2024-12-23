using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct BuilderProcessorSystem : ISystem
{
    [BurstCompile]
    unsafe void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var (processor, builderTurret) in
            SystemAPI.Query<RefRW<Processor>, RefRW<BuilderTurret>>()
            .WithAll<Builder>())
        {
            MappedMemory* mapped = (MappedMemory*)((nint)Unsafe.AsPointer(ref processor.ValueRW.Memory) + Processor.MappedMemoryStart);

            if (mapped->CombatTurret.InputShoot != 0)
            {
                builderTurret.ValueRW.ShootRequested = true;
                mapped->CombatTurret.InputShoot = 0;
            }

            RefRW<LocalTransform> turretTransform = SystemAPI.GetComponentRW<LocalTransform>(builderTurret.ValueRO.Turret);

            turretTransform.ValueRO.Rotation.ToEuler(out float3 turretEuler);

            mapped->CombatTurret.TurretCurrentRotation = turretEuler.y;
            mapped->CombatTurret.TurretCurrentAngle = 0f;

            if (float.IsFinite(mapped->CombatTurret.TurretTargetRotation))
            {
                float y = Utils.MoveTowardsAngle(turretEuler.y, mapped->CombatTurret.TurretTargetRotation, builderTurret.ValueRO.TurretRotationSpeed * SystemAPI.Time.DeltaTime);
                turretTransform.ValueRW.Rotation = quaternion.EulerXYZ(0, y, 0);
            }
        }
    }
}
