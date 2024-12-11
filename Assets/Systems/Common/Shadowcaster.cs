/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public class Shadowcaster
{
    /// An iterator that transforms coordinates based on a single quadrant to all the others.
    /// 
    /// For an explanation in depth, refer to the link below.\n
    /// https://www.albertford.com/shadowcasting
    class QuadrantIterator
    {
        public enum ECardinal
        {
            East,
            North,
            West,
            South
        }

        public ECardinal Cardinal { get; set; } = ECardinal.East;
        public int2 OriginPoint { get; set; } = new int2();

        readonly FogOfWarSettings _settings;

        public QuadrantIterator(FogOfWarSettings fogWar)
        {
            _settings = fogWar;
        }

        public int2 QuadrantToLevel(int2 quadrantVector)
        {
            int2 quadrantPoint = Cardinal switch
            {
                ECardinal.North => new int2(OriginPoint.x - quadrantVector.y, OriginPoint.y + quadrantVector.x),
                ECardinal.West => new int2(OriginPoint.x - quadrantVector.x, OriginPoint.y - quadrantVector.y),
                ECardinal.South => new int2(OriginPoint.x + quadrantVector.y, OriginPoint.y - quadrantVector.x),
                _ => new int2(OriginPoint.x + quadrantVector.x, OriginPoint.y + quadrantVector.y),
            };
            return _settings.WorldToLevel(_settings.GetWorldVector(quadrantPoint));
        }
    }

    class ColumnIterator
    {
        // In my implementaion, we consider the depth as the x axis of the eastern quadrant
        public int Depth { get; set; } = 0;
        public int MaxDepth { get; set; } = 0;

        // The variable startSlope is the 'lower' one, and the endSlope is the 'higher' one
        public float StartSlope { get; set; } = 0;
        public float EndSlope { get; set; } = 0;

        public ColumnIterator(int depth, int maxDepth, float startSlope, float endSlope)
        {
            Depth = depth;
            MaxDepth = maxDepth;
            StartSlope = startSlope;
            EndSlope = endSlope;
        }

        public List<int2> GetTiles()
        {
            List<int2> quadrantPoints = new();

            int minRow = Mathf.RoundToInt(Depth * StartSlope);
            int maxRow = Mathf.RoundToInt(Depth * EndSlope);

            for (int i = minRow; i < maxRow + 1; i++)
            {
                quadrantPoints.Add(new int2(Depth, i));
            }

            if (EndSlope == 1)
            {
                quadrantPoints.RemoveAt(quadrantPoints.Count - 1);
            }

            return quadrantPoints;
        }

        public bool IsProceedable() => Depth < MaxDepth;

        public void ProceedIfPossible()
        {
            if (Depth < MaxDepth) Depth += 1;
        }
    }

    public readonly FogField FogField = new();

    [NotNull] readonly FogOfWarSettings? _settings;
    [NotNull] readonly LevelData _levelData;
    // We declare this here to prevent creating and destroying the same object over and over
    [NotNull] readonly QuadrantIterator? _quadrantIterator;

    public Shadowcaster(FogOfWarSettings settings, LevelData levelData)
    {
        _settings = settings;
        _levelData = levelData;

        for (int xIterator = 0; xIterator < _settings.LevelDimensionX; xIterator++)
        {
            FogField.AddColumn(new LevelColumnVisibility(
                Enumerable.Repeat(ETileVisibility.Hidden, _settings.LevelDimensionY)));
        }

        _quadrantIterator = new QuadrantIterator(_settings);
    }

    /// <summary>
    /// Resets all tile's visibility info.
    /// </summary>
    public void ResetTileVisibility() => FogField.Reset(_settings);

    /// <summary>
    /// Processes the level data with shadowcasting algorithm, and updates the FogField object accordingly
    /// </summary>
    public void ProcessLevelData(int2 revealerPoint, int sightRange)
    {
        // Reveal the first tile where the revealer is at
        RevealTile(_settings.WorldToLevel(_settings.GetWorldVector(revealerPoint)));

        // Give the quadrant iterator the revealer's position
        _quadrantIterator.OriginPoint = revealerPoint;

        // We deal with 90 degrees each, anti-clockwise, starting from east to west
        foreach (int cardinal in System.Enum.GetValues(typeof(QuadrantIterator.ECardinal)))
        {
            _quadrantIterator.Cardinal = (QuadrantIterator.ECardinal)cardinal;

            // Here goes the BFS algorithm, we queue a new pass during each pass if needed, then start the new one
            Queue<ColumnIterator> columnIterators = new();

            // The first pass of the given quadrant, start from slope -1 to slope 1
            columnIterators.Enqueue(new ColumnIterator(1, sightRange, -1, 1));

            while (columnIterators.Count > 0)
            {
                ColumnIterator columnIterator = columnIterators.Dequeue();

                // Note that the given points may have negative y values instead of starting from zero
                List<int2> quadrantPoints = columnIterator.GetTiles();

                // This is to detect points where the obstacle tile and the empty tile are adjacent
                int2 lastQuadrantPoint = default;

                // This is to skip the first pass where the lastQuadrantPoint variable is not assigned yet
                bool firstStepFlag = true;

                foreach (int2 quadrantPoint in quadrantPoints)
                {
                    if (IsTileObstacle(quadrantPoint) || IsTileVisible(columnIterator, quadrantPoint))
                    {
                        RevealTileIteratively(quadrantPoint, sightRange);
                    }

                    if (firstStepFlag == false)
                    {
                        if (IsTileObstacle(lastQuadrantPoint) && IsTileEmpty(quadrantPoint))
                        {
                            columnIterator.StartSlope = GetQuadrantSlope(quadrantPoint);
                        }

                        if (IsTileEmpty(lastQuadrantPoint) && IsTileObstacle(quadrantPoint))
                        {
                            if (columnIterator.IsProceedable() == false)
                            {
                                continue;
                            }

                            ColumnIterator nextColumnIterator = new(
                                columnIterator.Depth,
                                sightRange,
                                columnIterator.StartSlope,
                                GetQuadrantSlope(quadrantPoint));

                            nextColumnIterator.ProceedIfPossible();

                            columnIterators.Enqueue(nextColumnIterator);
                        }
                    }

                    lastQuadrantPoint = quadrantPoint;

                    firstStepFlag = false;
                }

                if (IsTileEmpty(lastQuadrantPoint) && columnIterator.IsProceedable())
                {
                    columnIterator.ProceedIfPossible();

                    columnIterators.Enqueue(columnIterator);
                }
            }
        }
    }

    void RevealTileIteratively(int2 quadrantPoint, int sightRange)
    {
        int2 levelCoordinates = _quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (_settings.CheckLevelGridRange(levelCoordinates) == false)
        { return; }

        if (math.length(quadrantPoint) > sightRange)
        { return; }

        LevelColumnVisibility? column = FogField[levelCoordinates.x];
        if (column != null) column[levelCoordinates.y] = ETileVisibility.Revealed;
    }

    void RevealTile(int2 levelCoordinates)
    {
        if (_settings.CheckLevelGridRange(levelCoordinates) == false)
        { return; }

        LevelColumnVisibility? column = FogField[levelCoordinates.x];
        if (column != null) column[levelCoordinates.y] = ETileVisibility.Revealed;
    }

    bool IsTileEmpty(int2 quadrantPoint)
    {
        int2 levelCoordinates = _quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (_settings.CheckLevelGridRange(levelCoordinates) == false)
        { return true; }

        return _levelData[levelCoordinates.x]?[levelCoordinates.y] == ETileState.Empty;
    }

    bool IsTileObstacle(int2 quadrantPoint)
    {
        int2 levelCoordinates = _quadrantIterator.QuadrantToLevel(quadrantPoint);

        if (_settings.CheckLevelGridRange(levelCoordinates) == false)
        { return false; }

        return _levelData[levelCoordinates.x]?[levelCoordinates.y] == ETileState.Obstacle;
    }

    bool IsTileVisible(ColumnIterator columnIterator, int2 quadrantPoint) =>
        (quadrantPoint.y >= columnIterator.Depth * columnIterator.StartSlope) &&
        (quadrantPoint.y <= columnIterator.Depth * columnIterator.EndSlope);

    // The reason that this is not simply y / x is that the wall is diamond-shaped, refer to the links at the top
    float GetQuadrantSlope(int2 quadrantPoint) =>
        ((quadrantPoint.y * 2) - 1) / ((float)quadrantPoint.x * 2);
}
