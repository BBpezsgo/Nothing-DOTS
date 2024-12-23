using Unity.Entities;

public struct CombatTurret : IComponentData
{
    public Entity Turret;
    public Entity Cannon;
    public Entity ProjectilePrefab;
    public Entity ShootPosition;
    
    public bool ShootRequested;

    public float TurretRotationSpeed;
    public float CannonRotationSpeed;
}
