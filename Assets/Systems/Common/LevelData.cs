/*
 * Created :    Winter 2022
 * Author :     SeungGeon Kim (keithrek@hanmail.net)
 * Project :    FogWar
 * 
 * All Content (C) 2022 Unlimited Fischl Works, all rights reserved.
 */

using System.Collections;
using System.Collections.Generic;

/// <summary>
/// <seealso href="https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/aos-fog-of-war-249249">Source</seealso>
/// </summary>
public readonly struct LevelData : IReadOnlyList<LevelColumnType?>
{
    readonly List<LevelColumnType?> _levelRow;

    int IReadOnlyCollection<LevelColumnType?>.Count => _levelRow.Count;

    public LevelColumnType? this[int index]
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

    public LevelData() => _levelRow = new();

    public readonly void AddColumn(LevelColumnType levelColumn) => _levelRow.Add(levelColumn);

    IEnumerator<LevelColumnType?> IEnumerable<LevelColumnType?>.GetEnumerator() => _levelRow.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _levelRow.GetEnumerator();
}
