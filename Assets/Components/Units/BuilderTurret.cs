using Unity.Entities;

public struct BuilderTurret : IComponentData
{
    public Entity Turret;
    public bool ShootRequested;
    public float TurretRotationSpeed;
}
