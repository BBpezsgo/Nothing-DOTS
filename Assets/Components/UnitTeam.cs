using Unity.Entities;
using Unity.NetCode;

public struct UnitTeam : IComponentData
{
    [GhostField] public int Team;
}
