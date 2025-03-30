using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

class TerminalEmulator
{
    readonly Label _label;
    readonly StringBuilder _stdout;
    readonly Queue<char> _stdin;

    public TerminalEmulator(Label label)
    {
        _label = label;
        _stdout = new StringBuilder();
        _stdin = new();
    }

    public char RequestKey() => _stdin.TryDequeue(out char c) ? c : default;

    public void Update()
    {
        if (!_label.visible || _label.panel.focusController.focusedElement != _label) return;

        if (Keyboard.current.digit0Key.wasPressedThisFrame) _stdin.Enqueue('0');
        else if (Keyboard.current.digit1Key.wasPressedThisFrame) _stdin.Enqueue('1');
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) _stdin.Enqueue('2');
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) _stdin.Enqueue('3');
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) _stdin.Enqueue('4');
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) _stdin.Enqueue('5');
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) _stdin.Enqueue('6');
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) _stdin.Enqueue('7');
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) _stdin.Enqueue('8');
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) _stdin.Enqueue('9');
        else if (Keyboard.current.aKey.wasPressedThisFrame) _stdin.Enqueue('a');
        else if (Keyboard.current.bKey.wasPressedThisFrame) _stdin.Enqueue('b');
        else if (Keyboard.current.cKey.wasPressedThisFrame) _stdin.Enqueue('c');
        else if (Keyboard.current.dKey.wasPressedThisFrame) _stdin.Enqueue('d');
        else if (Keyboard.current.eKey.wasPressedThisFrame) _stdin.Enqueue('e');
        else if (Keyboard.current.fKey.wasPressedThisFrame) _stdin.Enqueue('f');
        else if (Keyboard.current.gKey.wasPressedThisFrame) _stdin.Enqueue('g');
        else if (Keyboard.current.hKey.wasPressedThisFrame) _stdin.Enqueue('h');
        else if (Keyboard.current.iKey.wasPressedThisFrame) _stdin.Enqueue('i');
        else if (Keyboard.current.jKey.wasPressedThisFrame) _stdin.Enqueue('j');
        else if (Keyboard.current.kKey.wasPressedThisFrame) _stdin.Enqueue('k');
        else if (Keyboard.current.lKey.wasPressedThisFrame) _stdin.Enqueue('l');
        else if (Keyboard.current.mKey.wasPressedThisFrame) _stdin.Enqueue('m');
        else if (Keyboard.current.nKey.wasPressedThisFrame) _stdin.Enqueue('n');
        else if (Keyboard.current.oKey.wasPressedThisFrame) _stdin.Enqueue('o');
        else if (Keyboard.current.pKey.wasPressedThisFrame) _stdin.Enqueue('p');
        else if (Keyboard.current.qKey.wasPressedThisFrame) _stdin.Enqueue('q');
        else if (Keyboard.current.rKey.wasPressedThisFrame) _stdin.Enqueue('r');
        else if (Keyboard.current.sKey.wasPressedThisFrame) _stdin.Enqueue('s');
        else if (Keyboard.current.tKey.wasPressedThisFrame) _stdin.Enqueue('t');
        else if (Keyboard.current.uKey.wasPressedThisFrame) _stdin.Enqueue('u');
        else if (Keyboard.current.vKey.wasPressedThisFrame) _stdin.Enqueue('v');
        else if (Keyboard.current.wKey.wasPressedThisFrame) _stdin.Enqueue('w');
        else if (Keyboard.current.xKey.wasPressedThisFrame) _stdin.Enqueue('x');
        else if (Keyboard.current.yKey.wasPressedThisFrame) _stdin.Enqueue('y');
        else if (Keyboard.current.zKey.wasPressedThisFrame) _stdin.Enqueue('z');
        else if (Keyboard.current.spaceKey.wasPressedThisFrame) _stdin.Enqueue(' ');
        else if (Keyboard.current.enterKey.wasPressedThisFrame) _stdin.Enqueue('\r');
        else if (Keyboard.current.commaKey.wasPressedThisFrame) _stdin.Enqueue(',');
        else if (Keyboard.current.periodKey.wasPressedThisFrame) _stdin.Enqueue('.');
        else if (Keyboard.current.minusKey.wasPressedThisFrame) _stdin.Enqueue('-');
    }

    public void Feed(char value) => _stdout.Append(value);
    public void Feed(string value) => _stdout.Append(value);
    public void Feed(StringBuilder value) => _stdout.Append(value);
    public void Clear() => _stdout.Clear();

    public void Render()
    {
        _label.text = _stdout.ToString();
    }
}
