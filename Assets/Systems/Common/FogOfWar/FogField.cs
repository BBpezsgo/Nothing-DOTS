/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public readonly struct FogField
{
    readonly int _width;
    readonly int _height;
    readonly TileVisibility[] _cells;
    readonly Color32[] _colors;

    public ref TileVisibility this[int2 position] => ref _cells[position.y * _width + position.x];

    public FogField(int width, int height)
    {
        _width = width;
        _height = height;
        _cells = new TileVisibility[width * height];
        _colors ??= new Color32[width * height];
    }

    public void Reset(bool keepRevealedTiles)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            ref TileVisibility cell = ref _cells[i];

            if (!keepRevealedTiles)
            { cell = TileVisibility.Hidden; }
            else if (cell == TileVisibility.Revealed)
            { cell = TileVisibility.PreviouslyRevealed; }
        }
    }

    public Color32[] GetColors(bool keepRevealedTiles, float revealedTileOpacity, float fogPlaneAlpha)
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                TileVisibility visibility = _cells[y * _width + x];

                float tileOpacity = 1 - (int)visibility;

                if (keepRevealedTiles &&
                    visibility == TileVisibility.PreviouslyRevealed)
                { tileOpacity = revealedTileOpacity; }

                _colors[(_height - y - 1) * _width + (_width - x - 1)] =
                    new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(tileOpacity * fogPlaneAlpha * byte.MaxValue));
            }
        }

        return _colors;
    }
}
