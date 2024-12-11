using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
[AddComponentMenu("Authoring/FogOfWarSettingsAuthoring")]
public class FogOfWarSettingsAuthoring : MonoBehaviour
{
    [SerializeField] public float3 levelMidPoint = default;

    [Range(1, 30)]
    [SerializeField] public float FogRefreshRate = 10;
    [SerializeField] public bool keepRevealedTiles = false;

    [Header("Fog Properties")]
    [Range(0, 100)]
    [SerializeField] public float fogPlaneHeight = 1;
    [SerializeField, NotNull] public Material? fogPlaneMaterial = null;
    [SerializeField] public Color fogColor = new Color32(5, 15, 25, 255);
    [Range(0, 1)]
    [SerializeField] public float fogPlaneAlpha = 1;
    [Range(1, 5)]
    [SerializeField] public float fogLerpSpeed = 2.5f;
    [Range(1, 1)]
    [SerializeField] public float revealedTileOpacity = 0.5f;

    [Header("Scan Properties")]
    [Range(1, 128)]
    [Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
    [SerializeField] public int levelDimensionX = 11;
    [Range(1, 128)]
    [Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
    [SerializeField] public int levelDimensionY = 11;
    [SerializeField] public float unitScale = 1;
    [SerializeField] public float scanSpacingPerUnit = 0.25f;
    [SerializeField] public float rayStartHeight = 5;
    [SerializeField] public float rayMaxDistance = 10;
    [SerializeField] public LayerMask obstacleLayers = new LayerMask();
    [SerializeField] public bool ignoreTriggers = true;

    class Baker : Baker<FogOfWarSettingsAuthoring>
    {
        public override void Bake(FogOfWarSettingsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<FogOfWarSettings>(entity, new()
            {
                LevelMidPoint = authoring.levelMidPoint,

                FogRefreshRate = authoring.FogRefreshRate,
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
