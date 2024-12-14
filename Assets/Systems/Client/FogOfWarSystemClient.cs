using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class FogOfWarSystemClient : SystemBase
{
    [NotNull] FogOfWarSettings? _settings = default;
    [NotNull] Shadowcaster? _shadowcaster = default;
    GameObject? _fogPlane;
    float _fogRefreshRateTimer;
    Texture2D? _fogPlaneTextureLerpTarget;
    Texture2D? _fogPlaneTextureLerpBuffer;
    [NotNull] TileState[]? _levelData = default;

    protected override void OnCreate()
    {
        RequireForUpdate<FogOfWarSettings>();
        RequireForUpdate<FogRevealer>();
    }

    protected override void OnStartRunning()
    {
        _settings = SystemAPI.ManagedAPI.GetSingleton<FogOfWarSettings>();
        _levelData = new TileState[_settings.LevelDimensionX * _settings.LevelDimensionY];
        _shadowcaster = new Shadowcaster(_settings, _levelData);


        var map = QuadrantSystem.GetMap(World.Unmanaged);
        for (int x = 0; x < _settings.LevelDimensionX; x++)
        {
            for (int y = 0; y < _settings.LevelDimensionY; y++)
            {
                Collider collider = new(true, new AABBCollider()
                {
                    AABB = new AABB()
                    {
                        Center = default,
                        Extents = new float3(
                            (_settings.UnitScale - _settings.ScanSpacingPerUnit) / 2.0f,
                            _settings.UnitScale / 2.0f,
                            (_settings.UnitScale - _settings.ScanSpacingPerUnit) / 2.0f)
                    },
                });
                var point = new float3(
                    _settings.GetWorldX(x),
                    _settings.LevelMidPoint.y,
                    _settings.GetWorldY(y));
                bool isObstacleHit = CollisionSystem.Intersect(map, collider, point,
                    out _, out _);
                // bool isObstacleHit = Physics.BoxCast(
                //     new float3(
                //         _settings.GetWorldX(x),
                //         _settings.LevelMidPoint.y + _settings.RayStartHeight,
                //         _settings.GetWorldY(y)),
                //     new float3(
                //         (_settings.UnitScale - _settings.ScanSpacingPerUnit) / 2.0f,
                //         _settings.UnitScale / 2.0f,
                //         (_settings.UnitScale - _settings.ScanSpacingPerUnit) / 2.0f),
                //     (float3)Vector3.down,
                //     Quaternion.identity,
                //     _settings.RayMaxDistance,
                //     _settings.ObstacleLayers,
                //     (QueryTriggerInteraction)(2 - Convert.ToInt32(_settings.IgnoreTriggers)));

                if (isObstacleHit)
                {
                    _levelData[y * _settings.LevelDimensionX + x] = TileState.Obstacle;
                    CollisionSystem.Debug(collider, point, Color.red, 100f, false);
                }
                else
                {
                    CollisionSystem.Debug(collider, point, Color.green, 100f, false);
                }
            }
        }

        InitializeFog();
    }

    protected override void OnUpdate()
    {
        _fogRefreshRateTimer += SystemAPI.Time.DeltaTime;

        if (_fogRefreshRateTimer < 1 / _settings.FogRefreshRate)
        { return; }

        _fogRefreshRateTimer -= 1 / _settings.FogRefreshRate;

        _shadowcaster.ResetTileVisibility();

        foreach (var (fogRevealer, transform) in
            SystemAPI.Query<RefRW<FogRevealer>, RefRO<LocalToWorld>>())
        {
            int2 currentLevelCoordinates = _settings.GetUnit(transform.ValueRO.Position);
            fogRevealer.ValueRW.CurrentLevelCoordinates = currentLevelCoordinates;

            if (currentLevelCoordinates.Equals(fogRevealer.ValueRO.LastSeenAt))
            { continue; }

            fogRevealer.ValueRW.LastSeenAt = currentLevelCoordinates;

            _shadowcaster.ProcessLevelData(
                fogRevealer.ValueRO.CurrentLevelCoordinates,
                (int)math.round(fogRevealer.ValueRO.SightRange / _settings.UnitScale));
        }

        UpdateFogPlaneTextureTarget();
        UpdateFogPlaneTextureBuffer();
    }

    void InitializeFog()
    {
        _fogPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);

        _fogPlane.name = "[RUNTIME] Fog_Plane";

        _fogPlane.transform.position = new Vector3(
            _settings.LevelMidPoint.x,
            _settings.LevelMidPoint.y + _settings.FogPlaneHeight,
            _settings.LevelMidPoint.z);

        _fogPlane.transform.localScale = new Vector3(
            _settings.LevelDimensionX * _settings.UnitScale / 10.0f,
            1,
            _settings.LevelDimensionY * _settings.UnitScale / 10.0f);

        _fogPlaneTextureLerpTarget = new Texture2D(_settings.LevelDimensionX, _settings.LevelDimensionY);
        _fogPlaneTextureLerpBuffer = new Texture2D(_settings.LevelDimensionX, _settings.LevelDimensionY)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        _fogPlane.GetComponent<MeshRenderer>().material = new Material(_settings.FogPlaneMaterial);
        _fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", _fogPlaneTextureLerpBuffer);
        GameObject.Destroy(_fogPlane.GetComponent<MeshCollider>());
    }

    void UpdateFogPlaneTextureBuffer()
    {
        if (_fogPlaneTextureLerpBuffer == null ||
            _fogPlaneTextureLerpTarget == null) return;

        _fogPlaneTextureLerpBuffer.CopyPixels(_fogPlaneTextureLerpTarget);
        _fogPlaneTextureLerpBuffer.Apply();

        return;

        // FIXME: This takes 40ms every frame

        Color32[] bufferPixels = _fogPlaneTextureLerpBuffer.GetPixels32();
        Color32[] targetPixels = _fogPlaneTextureLerpTarget.GetPixels32();

        if (bufferPixels.Length != targetPixels.Length)
        {
            Debug.LogError("Fog plane texture buffer and target have different pixel counts");
            return;
        }

        for (int i = 0; i < bufferPixels.Length; i++)
        {
            bufferPixels[i] = Color32.Lerp(bufferPixels[i], targetPixels[i], _settings.FogLerpSpeed * SystemAPI.Time.DeltaTime);
        }

        _fogPlaneTextureLerpBuffer.SetPixels32(bufferPixels);

        _fogPlaneTextureLerpBuffer.Apply();
    }

    void UpdateFogPlaneTextureTarget()
    {
        if (_fogPlaneTextureLerpTarget == null || _fogPlane == null) return;

        _fogPlane.GetComponent<MeshRenderer>().material.SetColor("_Color", _settings.FogColor);

        _fogPlaneTextureLerpTarget.SetPixels32(_shadowcaster.FogField.GetColors(_settings.KeepRevealedTiles, _settings.RevealedTileOpacity, _settings.FogPlaneAlpha));

        _fogPlaneTextureLerpTarget.Apply();
    }

    /// <summary>
    /// Checks if the given pair of world coordinates and additionalRadius is visible by FogRevealers.
    /// </summary>
    public bool CheckVisibility(float3 worldCoordinates, int additionalRadius)
    {
        int2 levelCoordinates = _settings.WorldToLevel(worldCoordinates);

        if (additionalRadius == 0)
        {
            return
                _shadowcaster.FogField[levelCoordinates] ==
                TileVisibility.Revealed;
        }

        int scanResult = 0;

        for (int xIterator = -1; xIterator < additionalRadius + 1; xIterator++)
        {
            for (int yIterator = -1; yIterator < additionalRadius + 1; yIterator++)
            {
                int2 currentPoint = new(
                    levelCoordinates.x + xIterator,
                    levelCoordinates.y + yIterator);

                if (!_settings.CheckLevelGridRange(currentPoint))
                {
                    scanResult = 0;
                    break;
                }

                scanResult += Convert.ToInt32(_shadowcaster.FogField[currentPoint] == TileVisibility.Revealed);
            }
        }

        return scanResult > 0;
    }
}
