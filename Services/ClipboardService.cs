using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FluentClip.Services;

public class ClipboardService : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _isMonitoring;

    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public event EventHandler? ClipboardChanged;

    public void StartMonitoring(Window window)
    {
        if (_isMonitoring) return;

        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _hwnd = helper.Handle;

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        AddClipboardFormatListener(_hwnd);
        _isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        RemoveClipboardFormatListener(_hwnd);
        _hwndSource?.RemoveHook(WndProc);
        _isMonitoring = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
