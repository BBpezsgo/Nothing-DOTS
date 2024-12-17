using Unity.Entities;

public struct BuilderTurret : IComponentData
{
    public bool ShootRequested;

    public float TargetRotation;
    public float TargetAngle;
}
