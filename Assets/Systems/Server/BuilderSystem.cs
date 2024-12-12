using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct BuilderSystem : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        var map = QuadrantSystem.GetMap(ref state);

        foreach (var (turret, localToWorld) in
                    SystemAPI.Query<RefRW<BuilderTurret>, RefRO<LocalToWorld>>())
        {
            if (!turret.ValueRO.ShootRequested) continue;
            turret.ValueRW.ShootRequested = false;

            Ray ray = new(localToWorld.ValueRO.Position, localToWorld.ValueRO.Position + (localToWorld.ValueRO.Up * Builder.BuildRadius), Layers.BuildingPlaceholder, false);

            Debug.DrawLine(ray.Start, ray.End, Color.white, 0.2f, false);

            if (!QuadrantRayCast.RayCast(map, ray, out Hit hit))
            { continue; }

            DebugEx.DrawPoint(ray.GetPoint(hit.Distance), 1f, Color.white, 0.2f, false);

            if (SystemAPI.HasComponent<BuildingPlaceholder>(hit.Entity.Entity))
            {
                RefRW<BuildingPlaceholder> building = SystemAPI.GetComponentRW<BuildingPlaceholder>(hit.Entity.Entity);
                building.ValueRW.CurrentProgress += 1f * SystemAPI.Time.DeltaTime;
                DebugEx.DrawPoint(ray.GetPoint(hit.Distance), 1f, Color.green, 0.2f, false);
                continue;
            }
        }
    }
}
