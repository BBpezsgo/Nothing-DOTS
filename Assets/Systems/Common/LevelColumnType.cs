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
public enum ETileState
{
    Empty,
    Obstacle
}

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public readonly struct LevelColumnType
{
    readonly ETileState[] _levelColumn;

    public ETileState this[int index]
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
                return ETileState.Empty;
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

    public LevelColumnType(IEnumerable<ETileState> stateTiles) => _levelColumn = stateTiles.ToArray();
}
