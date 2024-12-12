using Unity.Entities;

public struct BuilderTurret : IComponentData
{
    public Entity ShootPosition;
    
    public bool ShootRequested;

    public float TargetRotation;
    public float TargetAngle;
}
