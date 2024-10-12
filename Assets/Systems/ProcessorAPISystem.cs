using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

    void ISystem.OnUpdate(ref SystemState state)
    {
        _turretLookup.Update(ref state);
        _transformLookup.Update(ref state);

        foreach ((Processor processor, RefRW<LocalTransform> transform, Entity entity) in
                    SystemAPI.Query<Processor, RefRW<LocalTransform>>()
                    .WithEntityAccess())
        {
            System.Span<byte> memory = processor.MappedMemory;
            if (memory.IsEmpty) continue;

            // float3 translation = default;
            // translation.x = unchecked((sbyte)memory[0]);
            // translation.z = unchecked((sbyte)memory[1]);
            // transform.ValueRW.Position += translation * SystemAPI.Time.DeltaTime;

            if (state.EntityManager.HasBuffer<Child>(entity))
            {
                foreach (Child child in state.EntityManager.GetBuffer<Child>(entity))
                {
                    RefRW<Turret> turret = _turretLookup.GetRefRWOptional(child.Value);
                    if (!turret.IsValid) continue;
                    RefRW<LocalTransform> turretTransform = _transformLookup.GetRefRWOptional(child.Value);
                    if (!turretTransform.IsValid) continue;

                    if (memory[2] != 0)
                    {
                        turret.ValueRW.ShootRequested = true;
                        memory[2] = 0;
                    }

                    turret.ValueRW.TargetRotation = unchecked((sbyte)memory[3]);
                    turret.ValueRW.TargetAngle = unchecked((sbyte)memory[4]);

                    float3 euler = math.EulerXYZ(turretTransform.ValueRO.Rotation);
                    sbyte currentRotation = (sbyte)math.degrees(euler.x);
                    sbyte currentAngle = (sbyte)math.degrees(euler.y);
                    memory[5] = unchecked((byte)currentRotation);
                    memory[6] = unchecked((byte)currentAngle);

                    break;
                }
            }
        }
    }
}
