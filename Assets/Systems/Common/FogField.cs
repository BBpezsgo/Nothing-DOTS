/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using System.Collections.Generic;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public class FogField
{
    readonly List<LevelColumnVisibility?> _levelRow = new();
    Color[]? _colors = null;

    public LevelColumnVisibility? this[int index]
    {
        get
        {
            if (index >= 0 && index < _levelRow.Count)
            {
                return _levelRow[index];
            }
            else
            {
                Debug.LogError("Index given in x axis is out of range");
                return null;
            }
        }
        set
        {
            if (index >= 0 && index < _levelRow.Count)
            {
                _levelRow[index] = value;
            }
            else
            {
                Debug.LogError("Index given in x axis is out of range");
            }
        }
    }

    public void AddColumn(LevelColumnVisibility levelColumn)
    {
        _levelRow.Add(levelColumn);
    }

    public void Reset(FogOfWarSettings settings)
    {
        foreach (LevelColumnVisibility? levelColumn in _levelRow)
        {
            levelColumn?.Reset(settings);
        }
    }

    public Color[] GetColors(float fogPlaneAlpha, FogOfWarSettings settings)
    {
        int h = _levelRow.Count;
        int w = _levelRow[0]!.Count;
        _colors ??= new Color[h * w];

        for (int xIterator = 0; xIterator < w; xIterator++)
        {
            for (int yIterator = 0; yIterator < h; yIterator++)
            {
                int visibility = (int)(
                    _levelRow[yIterator]?[w - 1 - xIterator] ??
                    ETileVisibility.Hidden);

                float tileOpacity = 1 - visibility;

                if (settings.KeepRevealedTiles && visibility == (int)ETileVisibility.PreviouslyRevealed)
                {
                    tileOpacity = settings.RevealedTileOpacity;
                }

                // The reason that the darker side is the revealed ones is to let users customize fog's color
                _colors[h * (xIterator + 1) - (yIterator + 1)] =
                    new Color(1, 1, 1, tileOpacity * fogPlaneAlpha);
            }
        }

        return _colors;
    }
}
