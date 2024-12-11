/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public enum ETileVisibility
{
    Hidden,
    Revealed,
    PreviouslyRevealed
}

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public class LevelColumnVisibility
{
    readonly ETileVisibility[] _levelColumn;

    public int Count => _levelColumn.Length;

    public ETileVisibility this[int index]
    {
        get
        {
            if (index >= 0 && index < _levelColumn.Length)
            {
                return _levelColumn[index];
            }
            else
            {
                Debug.LogError("Index given in y axis is out of range");
                return ETileVisibility.Hidden;
            }
        }
        set
        {
            if (index >= 0 && index < _levelColumn.Length)
            {
                _levelColumn[index] = value;
            }
            else
            {
                Debug.LogError("Index given in y axis is out of range");
            }
        }
    }

    public LevelColumnVisibility(IEnumerable<ETileVisibility> visibilityTiles) => _levelColumn = visibilityTiles.ToArray();

    public void Reset(FogOfWarSettings settings)
    {
        for (int i = 0; i < _levelColumn.Length; i++)
        {
            if (settings.KeepRevealedTiles == false)
            {
                _levelColumn[i] = ETileVisibility.Hidden;

                continue;
            }

            if (_levelColumn[i] == ETileVisibility.Revealed)
            {
                _levelColumn[i] = ETileVisibility.PreviouslyRevealed;
            }
        }
    }
}
