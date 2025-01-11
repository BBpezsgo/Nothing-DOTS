using System;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

class TerminalEmulator
{
    readonly Label _label;
    readonly StringBuilder _buffer;
    char _currentKey;

    public TerminalEmulator(Label label)
    {
        _label = label;
        _buffer = new StringBuilder();
    }

    public char RequestKey()
    {
        char c = _currentKey;
        _currentKey = default;
        return c;
    }

    public void Update()
    {
        if (!_label.visible) return;

        if (Keyboard.current.digit0Key.wasPressedThisFrame) SendKey('0');
        else if (Keyboard.current.digit1Key.wasPressedThisFrame) SendKey('1');
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SendKey('2');
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SendKey('3');
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SendKey('4');
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) SendKey('5');
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) SendKey('6');
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) SendKey('7');
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) SendKey('8');
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) SendKey('9');
        else if (Keyboard.current.aKey.wasPressedThisFrame) SendKey('a');
        else if (Keyboard.current.bKey.wasPressedThisFrame) SendKey('b');
        else if (Keyboard.current.cKey.wasPressedThisFrame) SendKey('c');
        else if (Keyboard.current.dKey.wasPressedThisFrame) SendKey('d');
        else if (Keyboard.current.eKey.wasPressedThisFrame) SendKey('e');
        else if (Keyboard.current.fKey.wasPressedThisFrame) SendKey('f');
        else if (Keyboard.current.gKey.wasPressedThisFrame) SendKey('g');
        else if (Keyboard.current.hKey.wasPressedThisFrame) SendKey('h');
        else if (Keyboard.current.iKey.wasPressedThisFrame) SendKey('i');
        else if (Keyboard.current.jKey.wasPressedThisFrame) SendKey('j');
        else if (Keyboard.current.kKey.wasPressedThisFrame) SendKey('k');
        else if (Keyboard.current.lKey.wasPressedThisFrame) SendKey('l');
        else if (Keyboard.current.mKey.wasPressedThisFrame) SendKey('m');
        else if (Keyboard.current.nKey.wasPressedThisFrame) SendKey('n');
        else if (Keyboard.current.oKey.wasPressedThisFrame) SendKey('o');
        else if (Keyboard.current.pKey.wasPressedThisFrame) SendKey('p');
        else if (Keyboard.current.qKey.wasPressedThisFrame) SendKey('q');
        else if (Keyboard.current.rKey.wasPressedThisFrame) SendKey('r');
        else if (Keyboard.current.sKey.wasPressedThisFrame) SendKey('s');
        else if (Keyboard.current.tKey.wasPressedThisFrame) SendKey('t');
        else if (Keyboard.current.uKey.wasPressedThisFrame) SendKey('u');
        else if (Keyboard.current.vKey.wasPressedThisFrame) SendKey('v');
        else if (Keyboard.current.wKey.wasPressedThisFrame) SendKey('w');
        else if (Keyboard.current.xKey.wasPressedThisFrame) SendKey('x');
        else if (Keyboard.current.yKey.wasPressedThisFrame) SendKey('y');
        else if (Keyboard.current.zKey.wasPressedThisFrame) SendKey('z');
        else if (Keyboard.current.spaceKey.wasPressedThisFrame) SendKey(' ');
        else if (Keyboard.current.enterKey.wasPressedThisFrame) SendKey('\r');
        else if (Keyboard.current.commaKey.wasPressedThisFrame) SendKey(',');
        else if (Keyboard.current.periodKey.wasPressedThisFrame) SendKey('.');
        else if (Keyboard.current.minusKey.wasPressedThisFrame) SendKey('-');
    }

    void SendKey(char c)
    {
        _currentKey = c;
    }

    public void Feed(char value)
    {
        _buffer.Append(value);
    }

    public void Feed(string value)
    {
        _buffer.Append(value);
    }

    public void Feed(StringBuilder value)
    {
        _buffer.Append(value);
    }

    public void Clear()
    {
        _buffer.Clear();
    }

    public void Render()
    {
        _label.text = _buffer.ToString();
    }
}
