using System;
using System.Windows;
using Microsoft.Win32;

namespace FluentClip.Services;

public class WindowService
{
    private readonly Window _window;
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FluentClip";

    public WindowService(Window window)
    {
        _window = window;
    }

    public void Show()
    {
        _window.Show();
        _window.Activate();
    }

    public void Hide()
    {
        _window.Hide();
    }

    public void Minimize()
    {
        _window.WindowState = WindowState.Minimized;
    }

    public void Maximize()
    {
        if (_window.WindowState == WindowState.Maximized)
            _window.WindowState = WindowState.Normal;
        else
            _window.WindowState = WindowState.Maximized;
    }

    public void SetAlwaysOnTop(bool isOnTop)
    {
        _window.Topmost = isOnTop;
    }

    public bool IsAlwaysOnTop => _window.Topmost;

    public bool IsMaximized => _window.WindowState == WindowState.Maximized;

    public void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key != null)
            {
                if (enable)
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch { }
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public void Close()
    {
        _window.Close();
    }

    public void ForceClose()
    {
        Application.Current.Shutdown();
    }
}
