using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FluentClip.Services;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 0;

    public void Start(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    public int RegisterHotkey(string hotkeyString, Action callback)
    {
        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            return -1;

        int id = ++_currentId;
        if (RegisterHotKey(_hwnd, id, modifiers, vk))
        {
            _hotkeyActions[id] = callback;
            return id;
        }
        return -1;
    }

    public void UnregisterHotkey(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            UnregisterHotKey(_hwnd, id);
            _hotkeyActions.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _hotkeyActions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.ContainsKey(id))
            {
                _hotkeyActions[id]?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrEmpty(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+');
        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLower();
            switch (trimmed)
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    vk = GetVirtualKeyCode(trimmed);
                    break;
            }
        }

        return vk != 0;
    }

    private uint GetVirtualKeyCode(string key)
    {
        return key.ToUpper() switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59, "Z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73, "F5" => 0x74,
            "F6" => 0x75, "F7" => 0x76, "F8" => 0x77, "F9" => 0x78, "F10" => 0x79,
            "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20, "ENTER" => 0x0D, "ESCAPE" => 0x1B, "TAB" => 0x09,
            "BACKSPACE" => 0x08, "DELETE" => 0x2E, "INSERT" => 0x2D,
            "HOME" => 0x24, "END" => 0x23, "PAGEUP" => 0x21, "PAGEDOWN" => 0x22,
            "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
            _ => 0
        };
    }

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}
