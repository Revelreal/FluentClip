using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentClip.Models;

namespace FluentClip;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainWindow _mainWindow;
    private TextBox? _focusedHotkeyBox;

    public SettingsWindow(MainWindow mainWindow, AppSettings settings)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _settings = settings;
        LoadSettings();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
        }
        else
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void HotkeyTextBox_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _focusedHotkeyBox = sender as TextBox;
    }

    private void LoadSettings()
    {
        AutoStartCheckBox.IsChecked = _settings.AutoStart;
        MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
        RunInBackgroundCheckBox.IsChecked = _settings.RunInBackground;
        MonitorClipboardCheckBox.IsChecked = _settings.MonitorClipboard;
        
        if (_settings.DragActionCopy)
        {
            DragCopyRadio.IsChecked = true;
        }
        else
        {
            DragCutRadio.IsChecked = true;
        }
        
        ShowHotkeyTextBox.Text = _settings.ShowHotkey;
        SettingsHotkeyTextBox.Text = _settings.SettingsHotkey;
        PinHotkeyTextBox.Text = _settings.PinHotkey;
        
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        
        var modifiers = new System.Collections.Generic.List<string>();
        
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            modifiers.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            modifiers.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            modifiers.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            modifiers.Add("Win");
        
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }
        
        if (modifiers.Count == 0)
        {
            return;
        }
        
        string keyStr = key.ToString();
        
        if (keyStr.StartsWith("F") && int.TryParse(keyStr.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
        {
        }
        else if (keyStr.Length != 1 || !Regex.IsMatch(keyStr, @"[A-Z0-9]"))
        {
            if (!Enum.TryParse<Key>(keyStr, out _))
            {
                return;
            }
        }
        
        var hotkey = string.Join("+", modifiers) + "+" + keyStr;
        
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = hotkey;
        }
    }

    private void SaveSettings()
    {
        bool wasAutoStart = _settings.AutoStart;
        bool newAutoStart = AutoStartCheckBox.IsChecked ?? false;
        
        _settings.AutoStart = newAutoStart;
        _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
        _settings.RunInBackground = RunInBackgroundCheckBox.IsChecked ?? true;
        _settings.MonitorClipboard = MonitorClipboardCheckBox.IsChecked ?? true;
        _settings.DragActionCopy = DragCopyRadio.IsChecked ?? true;
        
        _settings.ShowHotkey = ShowHotkeyTextBox.Text;
        _settings.SettingsHotkey = SettingsHotkeyTextBox.Text;
        _settings.PinHotkey = PinHotkeyTextBox.Text;
        
        _settings.Save();
        
        if (newAutoStart && !wasAutoStart)
        {
            RequestAndSetAutoStart();
        }
        else if (!newAutoStart && wasAutoStart)
        {
            RemoveAutoStart();
        }
    }

    private void RequestAndSetAutoStart()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--set-autostart",
                UseShellExecute = true,
                Verb = "runas"
            };
            
            try
            {
                var regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (regKey != null)
                {
                    regKey.SetValue("FluentClip", $"\"{exePath}\"");
                    regKey.Close();
                }
            }
            catch
            {
                System.Diagnostics.Process.Start(startInfo);
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private void RemoveAutoStart()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                key.DeleteValue("FluentClip", false);
                key.Close();
            }
        }
        catch { }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
