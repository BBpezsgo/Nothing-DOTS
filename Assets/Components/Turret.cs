using Unity.Entities;

public enum TurretType
{
    Combat,
    Builder,
}

public struct Turret : IComponentData
{
    public TurretType Type;

    public Entity ProjectilePrefab;
    public Entity ShootPosition;
    
    public bool ShootRequested;

    public float TargetRotation;
    public float TargetAngle;
}
