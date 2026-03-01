using Unity.Entities;
using Unity.NetCode;

public struct CombatTurret : IComponentData
{
    public Entity Turret;
    public Entity Cannon;
    public int Projectile;
    public Entity ShootPosition;
    public int ShootEffect;

    public float MinAngle;
    public float MaxAngle;

    public float TurretRotationSpeed;
    public float CannonRotationSpeed;

    public float Spread;
    public float BulletReload;
    public float MagazineReload;
    public int MagazineSize;

    [GhostField] public int CurrentMagazineSize;
    public float BulletReloadProgress;
    public float MagazineReloadProgress;
}
