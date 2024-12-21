using Unity.Entities;

public struct Turret : IComponentData
{
    public Entity Cannon;
    public Entity ProjectilePrefab;
    public Entity ShootPosition;
    
    public bool ShootRequested;

    public float TurretRotationSpeed;
    public float CannonRotationSpeed;

    public float TargetRotation;
    public float TargetAngle;
}
