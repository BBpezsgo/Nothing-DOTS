using Unity.Entities;
using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
[UpdateBefore(typeof(TurretShootingSystemClient))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct CombatTurretSystemClient : ISystem
{
    [BurstCompile]
    void ISystem.OnUpdate(ref SystemState state)
    {
        foreach (var command in
            SystemAPI.Query<RefRO<ShootRpc>>()
            .WithAll<ReceiveRpcCommandRequest>())
        {
            foreach (var (ghostInstance, turret) in
                SystemAPI.Query<RefRO<GhostInstance>, RefRW<CombatTurret>>())
            {
                if (!command.ValueRO.Source.Equals(ghostInstance.ValueRO)) continue;

                turret.ValueRW.CurrentMagazineSize--;
                turret.ValueRW.BulletReloadProgress = 0f;

                if (turret.ValueRW.CurrentMagazineSize <= 0)
                {
                    turret.ValueRW.MagazineReloadProgress = 0f;
                }
                break;
            }
        }

        foreach (var turret in
            SystemAPI.Query<RefRW<CombatTurret>>())
        {
            if (turret.ValueRO.MagazineReloadProgress < turret.ValueRO.MagazineReload || turret.ValueRW.CurrentMagazineSize == 0)
            {
                turret.ValueRW.MagazineReloadProgress += SystemAPI.Time.DeltaTime;
                if (turret.ValueRO.MagazineReloadProgress >= turret.ValueRO.MagazineReload)
                {
                    turret.ValueRW.CurrentMagazineSize = turret.ValueRO.MagazineSize;
                }
            }

            if (turret.ValueRO.BulletReloadProgress < turret.ValueRO.BulletReload)
            {
                turret.ValueRW.BulletReloadProgress += SystemAPI.Time.DeltaTime;
            }
        }
    }
}
