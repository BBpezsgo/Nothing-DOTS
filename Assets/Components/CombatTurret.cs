using Unity.Entities;

public struct CombatTurret : IComponentData
{
    public Entity Turret;
    public Entity Cannon;
    public Entity ProjectilePrefab;
    public Entity ShootPosition;
    public int ShootEffect;

    public float MinAngle;
    public float MaxAngle;

    public float TurretRotationSpeed;
    public float CannonRotationSpeed;
}
