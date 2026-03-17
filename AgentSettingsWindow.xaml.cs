using System.Windows;
using System.Windows.Input;
using FluentClip.Models;
using Microsoft.Win32;
using System.Diagnostics;

namespace FluentClip;

public partial class AgentSettingsWindow : Window
{
    private readonly AgentSettings _settings;

    public AgentSettingsWindow(Window owner)
    {
        InitializeComponent();
        _settings = AgentSettings.Load();
        LoadSettings();
        Owner = owner;
        Closing += AgentSettingsWindow_Closing;
    }

    private void LoadSettings()
    {
        BaseUrlTextBox.Text = _settings.BaseUrl;
        ApiKeyBox.Password = _settings.ApiKey;
        ModelTextBox.Text = _settings.Model;
        StreamingCheckBox.IsChecked = _settings.UseStreaming;
        EnableToolCallsCheckBox.IsChecked = _settings.EnableToolCalls;
        SystemPromptTextBox.Text = _settings.SystemPrompt;
        AvatarPathText.Text = string.IsNullOrEmpty(_settings.AvatarPath) ? "未选择（将使用默认头像）" : _settings.AvatarPath;
    }

    private void AgentSettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void SaveSettings()
    {
        _settings.BaseUrl = BaseUrlTextBox.Text;
        _settings.ApiKey = ApiKeyBox.Password;
        _settings.Model = ModelTextBox.Text;
        _settings.UseStreaming = StreamingCheckBox.IsChecked ?? true;
        _settings.EnableToolCalls = EnableToolCallsCheckBox.IsChecked ?? false;
        _settings.SystemPrompt = SystemPromptTextBox.Text;
        _settings.Save();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void AvatarButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|所有文件|*.*",
            Title = "选择头像图片"
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.AvatarPath = dialog.FileName;
            AvatarPathText.Text = dialog.FileName;
        }
    }

    private void SubscribeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://platform.minimaxi.com/subscribe/coding-plan",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
