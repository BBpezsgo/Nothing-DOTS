/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public class Shadowcaster
{
    enum Cardinal
    {
        East,
        North,
        West,
        South,
    }

    /// An iterator that transforms coordinates based on a single quadrant to all the others.
    /// 
    /// For an explanation in depth, refer to the link below.\n
    /// https://www.albertford.com/shadowcasting
    readonly struct QuadrantIterator
    {
        public readonly Cardinal Cardinal;
        public readonly int2 OriginPoint;
        readonly FogOfWarSettings _settings;

        public QuadrantIterator(Cardinal cardinal, int2 originPoint, FogOfWarSettings settings)
        {
            Cardinal = cardinal;
            OriginPoint = originPoint;
            _settings = settings;
        }

        public int2 QuadrantToLevel(int2 quadrantVector)
        {
            int2 quadrantPoint = Cardinal switch
            {
                Cardinal.North => new int2(OriginPoint.x - quadrantVector.y, OriginPoint.y + quadrantVector.x),
                Cardinal.West => new int2(OriginPoint.x - quadrantVector.x, OriginPoint.y - quadrantVector.y),
                Cardinal.South => new int2(OriginPoint.x + quadrantVector.y, OriginPoint.y - quadrantVector.x),
                _ => new int2(OriginPoint.x + quadrantVector.x, OriginPoint.y + quadrantVector.y),
            };
            return _settings.WorldToLevel(_settings.GetWorld(quadrantPoint));
        }
    }

    readonly struct ColumnIterator
    {
        // In my implementaion, we consider the depth as the x axis of the eastern quadrant
        public readonly int Depth;
        public readonly int MaxDepth;

        // The variable startSlope is the 'lower' one, and the endSlope is the 'higher' one
        public readonly float StartSlope;
        public readonly float EndSlope;

        public ColumnIterator(int depth, int maxDepth, float startSlope, float endSlope)
        {
            Depth = depth;
            MaxDepth = maxDepth;
            StartSlope = startSlope;
            EndSlope = endSlope;
        }

        public readonly bool IsProceedable => Depth < MaxDepth;

        public readonly ColumnIterator ProceedIfPossible()
        {
            if (Depth < MaxDepth) return new(Depth + 1, MaxDepth, StartSlope, EndSlope);
            return this;
        }
    }

    public readonly FogField FogField;

    readonly FogOfWarSettings _settings;
    readonly TileState[] _levelData;
    readonly Queue<ColumnIterator> _columnIterators;

    public Shadowcaster(FogOfWarSettings settings, TileState[] levelData)
    {
        _settings = settings;
        _levelData = levelData;
        FogField = new(_settings.LevelDimensionX, _settings.LevelDimensionY);
        _columnIterators = new();
    }

    /// <summary>
    /// Resets all tile's visibility info.
    /// </summary>
    public void ResetTileVisibility() => FogField.Reset(_settings.KeepRevealedTiles);

    /// <summary>
    /// Processes the level data with shadowcasting algorithm, and updates the FogField object accordingly
    /// </summary>
    public void ProcessLevelData(int2 revealerPoint, int sightRange)
    {
        QuadrantIterator _quadrantIterator;

        // Reveal the first tile where the revealer is at
        RevealTile(_settings.WorldToLevel(_settings.GetWorld(revealerPoint)));

        ReadOnlySpan<Cardinal> cardinals = stackalloc Cardinal[]
        {
            Cardinal.East,
            Cardinal.North,
            Cardinal.West,
            Cardinal.South,
        };

        // We deal with 90 degrees each, anti-clockwise, starting from east to west
        for (int i = 0; i < cardinals.Length; i++)
        {
            _quadrantIterator = new QuadrantIterator(cardinals[i], revealerPoint, _settings);
            _columnIterators.Clear();

            // Here goes the BFS algorithm, we queue a new pass during each pass if needed, then start the new one

            // The first pass of the given quadrant, start from slope -1 to slope 1
            _columnIterators.Enqueue(new ColumnIterator(1, sightRange, -1, 1));

            while (_columnIterators.TryDequeue(out ColumnIterator columnIterator))
            {
                // Note that the given points may have negative y values instead of starting from zero

                // This is to detect points where the obstacle tile and the empty tile are adjacent
                int2 lastQuadrantPoint = default;

                // This is to skip the first pass where the lastQuadrantPoint variable is not assigned yet
                bool firstStepFlag = true;

                int minRow = Mathf.RoundToInt(columnIterator.Depth * columnIterator.StartSlope);
                int maxRow = Mathf.RoundToInt(columnIterator.Depth * columnIterator.EndSlope);
                if (columnIterator.EndSlope != 1) maxRow++;
                for (int _i = minRow; _i < maxRow; _i++)
                {
                    int2 quadrantPoint = new(columnIterator.Depth, _i);

                    if (IsTileObstacle(_quadrantIterator, quadrantPoint) || IsTileVisible(columnIterator, quadrantPoint))
                    {
                        RevealTileIteratively(_quadrantIterator, quadrantPoint, sightRange);
                    }

                    if (!firstStepFlag)
                    {
                        float nextIteratorStartSlope = columnIterator.StartSlope;

                        if (IsTileObstacle(_quadrantIterator, lastQuadrantPoint) && IsTileEmpty(_quadrantIterator, quadrantPoint))
                        {
                            nextIteratorStartSlope = GetQuadrantSlope(quadrantPoint);
                        }

                        if (IsTileEmpty(_quadrantIterator, lastQuadrantPoint) && IsTileObstacle(_quadrantIterator, quadrantPoint))
                        {
                            if (!columnIterator.IsProceedable) continue;

                            ColumnIterator nextColumnIterator = new(
                                columnIterator.Depth,
                                sightRange,
                                nextIteratorStartSlope,
                                GetQuadrantSlope(quadrantPoint));

                            nextColumnIterator = nextColumnIterator.ProceedIfPossible();

                            _columnIterators.Enqueue(nextColumnIterator);
                        }
                    }

                    lastQuadrantPoint = quadrantPoint;

                    firstStepFlag = false;
                }

                if (IsTileEmpty(_quadrantIterator, lastQuadrantPoint) && columnIterator.IsProceedable)
                {
                    columnIterator = columnIterator.ProceedIfPossible();

                    _columnIterators.Enqueue(columnIterator);
                }
            }
        }
    }

    void RevealTileIteratively(in QuadrantIterator quadrantIterator, int2 quadrantPoint, int sightRange)
    {
        int2 levelCoordinates = quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (!_settings.CheckLevelGridRange(levelCoordinates))
        { return; }

        if (math.length(quadrantPoint) > sightRange)
        { return; }

        FogField[levelCoordinates] = TileVisibility.Revealed;
    }

    void RevealTile(int2 levelCoordinates)
    {
        if (!_settings.CheckLevelGridRange(levelCoordinates))
        { return; }

        FogField[levelCoordinates] = TileVisibility.Revealed;
    }

    bool IsTileEmpty(in QuadrantIterator quadrantIterator, int2 quadrantPoint)
    {
        int2 levelCoordinates = quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (!_settings.CheckLevelGridRange(levelCoordinates))
        { return true; }

        return _levelData[levelCoordinates.y * _settings.LevelDimensionX + levelCoordinates.x] == TileState.Empty;
    }

    bool IsTileObstacle(in QuadrantIterator quadrantIterator, int2 quadrantPoint)
    {
        int2 levelCoordinates = quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (!_settings.CheckLevelGridRange(levelCoordinates))
        { return false; }

        return _levelData[levelCoordinates.y * _settings.LevelDimensionX + levelCoordinates.x] == TileState.Obstacle;
    }

    static bool IsTileVisible(ColumnIterator columnIterator, int2 quadrantPoint) =>
        (quadrantPoint.y >= columnIterator.Depth * columnIterator.StartSlope) &&
        (quadrantPoint.y <= columnIterator.Depth * columnIterator.EndSlope);

    // The reason that this is not simply y / x is that the wall is diamond-shaped, refer to the links at the top
    static float GetQuadrantSlope(int2 quadrantPoint) =>
        ((quadrantPoint.y * 2) - 1) / ((float)quadrantPoint.x * 2);
}
