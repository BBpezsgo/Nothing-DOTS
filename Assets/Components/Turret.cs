using Unity.Entities;

public struct Turret : IComponentData
{
    public Entity ProjectilePrefab;
    public Entity ShootPosition;
    public bool ShootRequested;
    public float TargetRotation;
    public float TargetAngle;
}
