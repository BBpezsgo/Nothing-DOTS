using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
[AddComponentMenu("Authoring/Fog of War Settings")]
class FogOfWarSettingsAuthoring : MonoBehaviour
{
    [SerializeField] float3 levelMidPoint = default;

    [Min(0f)]
    [SerializeField] float fogRefreshRate = 10f;
    [SerializeField] bool keepRevealedTiles = false;

    [Header("Fog Properties")]
    [SerializeField] float fogPlaneHeight = 1f;
    [SerializeField, NotNull] Material? fogPlaneMaterial = default;
    [SerializeField] Color fogColor = new Color32(5, 15, 25, 255);
    [Range(0f, 1f)]
    [SerializeField] float fogPlaneAlpha = 1f;
    [Min(0f)]
    [SerializeField] float fogLerpSpeed = 2.5f;
    [Range(0f, 1f)]
    [SerializeField] float revealedTileOpacity = 0.5f;

    [Header("Scan Properties")]
    [Min(1)]
    [Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
    [SerializeField] int levelDimensionX = 11;
    [Min(1)]
    [Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
    [SerializeField] int levelDimensionY = 11;
    [SerializeField] float unitScale = 1f;
    [SerializeField] float scanSpacingPerUnit = 0.25f;
    [SerializeField] float rayStartHeight = 5f;
    [Min(0f)]
    [SerializeField] float rayMaxDistance = 10f;
    [SerializeField] LayerMask obstacleLayers = default;
    [SerializeField] bool ignoreTriggers = true;

    class Baker : Baker<FogOfWarSettingsAuthoring>
    {
        public override void Bake(FogOfWarSettingsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<FogOfWarSettings>(entity, new()
            {
                LevelMidPoint = authoring.levelMidPoint,

                FogRefreshRate = authoring.fogRefreshRate,
                KeepRevealedTiles = authoring.keepRevealedTiles,

                FogPlaneHeight = authoring.fogPlaneHeight,
                FogColor = authoring.fogColor,
                FogPlaneMaterial = authoring.fogPlaneMaterial,
                FogPlaneAlpha = authoring.fogPlaneAlpha,
                FogLerpSpeed = authoring.fogLerpSpeed,
                RevealedTileOpacity = authoring.revealedTileOpacity,

                LevelDimensionX = authoring.levelDimensionX,
                LevelDimensionY = authoring.levelDimensionY,
                UnitScale = authoring.unitScale,
                ScanSpacingPerUnit = authoring.scanSpacingPerUnit,
                RayStartHeight = authoring.rayStartHeight,
                RayMaxDistance = authoring.rayMaxDistance,
                ObstacleLayers = authoring.obstacleLayers,
                IgnoreTriggers = authoring.ignoreTriggers,
            });
        }
    }
}
