using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public struct FogRevealer : IComponentData
{
    public int2 CurrentLevelCoordinates;
    public int SightRange;
    public bool UpdateOnlyOnMove;
    public int2 LastSeenAt;
}
