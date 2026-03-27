using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FluentClip.Models;
using FluentClip.Services;
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
        PersonaPromptTextBox.Text = _settings.PersonaPrompt;
        SystemPromptTextBox.Text = _settings.SystemPrompt;
        AvatarPathText.Text = string.IsNullOrEmpty(_settings.AvatarPath) ? "未选择（将使用默认头像）" : _settings.AvatarPath;
        AiWorkFolderTextBox.Text = _settings.AiWorkFolder;
        EnableShellExecutionCheckBox.IsChecked = _settings.EnableShellExecution;
        EnableMcpCheckBox.IsChecked = _settings.EnableMcp;

        // 根据 MCP 开关状态更新 UI
        UpdateMcpUiState();
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
        _settings.PersonaPrompt = PersonaPromptTextBox.Text;
        _settings.AiWorkFolder = AiWorkFolderTextBox.Text;
        _settings.EnableShellExecution = EnableShellExecutionCheckBox.IsChecked ?? false;
        _settings.EnableMcp = EnableMcpCheckBox.IsChecked ?? false;
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

    private void BrowseAiWorkFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择AI工作文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrEmpty(AiWorkFolderTextBox.Text) && Directory.Exists(AiWorkFolderTextBox.Text))
        {
            dialog.SelectedPath = AiWorkFolderTextBox.Text;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AiWorkFolderTextBox.Text = dialog.SelectedPath;
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

    private void EnableMcpCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMcpUiState();
    }

    private void UpdateMcpUiState()
    {
        var isEnabled = EnableMcpCheckBox.IsChecked ?? false;

        // 显示/隐藏 MCP 相关的 UI
        NodeStatusBorder.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        McpButtonsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        McpWarningText.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

        if (isEnabled)
        {
            // 启用时自动检测 Node.js
            CheckNodeEnvironment();
        }
        else
        {
            // 禁用时重置状态显示
            NodeStatusDot.Fill = new SolidColorBrush(Colors.Gray);
            NodeStatusText.Text = "MCP 已禁用";
            NodeVersionText.Visibility = Visibility.Collapsed;
        }
    }

    private void CheckNodeButton_Click(object sender, RoutedEventArgs e)
    {
        CheckNodeEnvironment();
    }

    private void Log(string message)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentClip", "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"node_install_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
            System.Diagnostics.Debug.WriteLine(logEntry);
        }
        catch { }
    }

    private void CheckNodeEnvironment()
    {
        Log("========== 开始检测 MCP 环境 ==========");
        try
        {
            NodeStatusText.Text = "正在检测...";
            NodeVersionText.Visibility = Visibility.Collapsed;

            // 1. 先检测 Python
            var pythonResult = RunCommand("python", "--version", 5000);
            Log($"Python 检测结果: ExitCode={pythonResult.exitCode}, Output='{pythonResult.output}'");

            if (pythonResult.exitCode != 0 || string.IsNullOrEmpty(pythonResult.output))
            {
                // Python 未安装
                Log("Python 未安装");
                NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                NodeStatusText.Text = "✗ Python 未安装";
                NodeVersionText.Text = "MCP 需要 Python 环境";
                NodeVersionText.Visibility = Visibility.Visible;
                InstallNodeButton.Content = "自动安装";
                InstallNodeButton.IsEnabled = true;
                Log("========== 检测完成 ==========");
                return;
            }

            var pythonVersion = pythonResult.output.Trim();
            Log($"Python 已安装，版本: {pythonVersion}");

            // 2. 检测 uv - Python 包运行工具
            var uvResult = RunCommand("uvx", "--version", 5000);
            Log($"uvx 检测结果: ExitCode={uvResult.exitCode}, Output='{uvResult.output}'");

            if (uvResult.exitCode == 0 && !string.IsNullOrEmpty(uvResult.output))
            {
                var uvVersion = uvResult.output.Trim();
                Log($"uvx 已就绪，版本: {uvVersion}");

                NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
                NodeStatusText.Text = "✓ MCP 环境已就绪";
                NodeVersionText.Text = $"Python: {pythonVersion} | uv: {uvVersion}";
                NodeVersionText.Visibility = Visibility.Visible;
                InstallNodeButton.Content = "已就绪";
                InstallNodeButton.IsEnabled = false;
            }
            else
            {
                Log("uv 未安装或检测失败");
                NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                NodeStatusText.Text = "✗ uv 未安装";
                NodeVersionText.Text = $"Python: {pythonVersion}，需要安装 uv";
                NodeVersionText.Visibility = Visibility.Visible;
                InstallNodeButton.Content = "自动安装";
                InstallNodeButton.IsEnabled = true;
            }
            Log("========== 检测完成 ==========");
        }
        catch (Exception ex)
        {
            Log($"检测异常: {ex.Message}");
            Log($"堆栈: {ex.StackTrace}");
            NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0));
            NodeStatusText.Text = "检测失败";
            NodeVersionText.Text = "请手动安装 Python 和 uv";
            NodeVersionText.Visibility = Visibility.Visible;
            InstallNodeButton.Content = "手动安装";
            InstallNodeButton.IsEnabled = true;
        }
    }

    private (int exitCode, string output) RunCommand(string fileName, string arguments, int timeoutMs)
    {
        try
        {
            // 对于有扩展名的文件（.exe, .bat, .cmd），直接运行
            // 对于没有扩展名的命令（如 node, npx, npm），通过 cmd.exe 执行
            var useCmd = !fileName.Contains('.');

            var startInfo = new ProcessStartInfo
            {
                FileName = useCmd ? "cmd.exe" : fileName,
                Arguments = useCmd ? $"/c {fileName} {arguments}" : arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // 添加用户本地 bin 目录到 PATH（uv 安装路径）
            var userLocalBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
            startInfo.Environment["PATH"] = $"{userLocalBin};{Environment.GetEnvironmentVariable("PATH")}";

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var hasExited = process.WaitForExit(timeoutMs);

            if (!hasExited)
            {
                Log($"命令超时: {fileName} {arguments}");
                try { process.Kill(); } catch { }
                return (-1, "命令执行超时");
            }

            return (process.ExitCode, output);
        }
        catch (Exception ex)
        {
            Log($"命令执行失败: {fileName} {arguments}, Error: {ex.Message}");
            return (-1, ex.Message);
        }
    }

    private void InstallNodeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 先检查 Python 是否已经安装
            Log("用户点击了安装按钮，先检测 Python 是否已安装...");
            var pythonCheck = RunCommand("python", "--version", 5000);
            Log($"Python 检测结果: ExitCode={pythonCheck.exitCode}, Output='{pythonCheck.output}'");

            if (pythonCheck.exitCode != 0 || string.IsNullOrEmpty(pythonCheck.output))
            {
                // Python 未安装，先安装 Python
                Log("Python 未安装，开始安装...");
                InstallMcpEnvironment(installPython: true, installUv: true);
            }
            else
            {
                // Python 已安装，检查 uv
                Log("Python 已安装，检查 uv...");
                var uvCheck = RunCommand("uvx", "--version", 5000);
                Log($"uvx 检测结果: ExitCode={uvCheck.exitCode}, Output='{uvCheck.output}'");

                if (uvCheck.exitCode == 0 && !string.IsNullOrEmpty(uvCheck.output))
                {
                    // uv 已安装，只刷新检测
                    Log("uv 已安装，刷新环境检测");
                    NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0));
                    NodeStatusText.Text = "正在检测环境...";
                    CheckNodeEnvironment();
                }
                else
                {
                    // uv 未安装，只安装 uv
                    Log("uv 未安装，开始安装 uv...");
                    InstallMcpEnvironment(installPython: false, installUv: true);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"安装异常: {ex.Message}");
            NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            NodeStatusText.Text = "安装失败";
            NodeVersionText.Text = ex.Message;
            NodeVersionText.Visibility = Visibility.Visible;
            InstallNodeButton.Content = "手动安装";
            InstallNodeButton.IsEnabled = true;
        }
    }

    private void InstallMcpEnvironment(bool installPython, bool installUv)
    {
        InstallNodeButton.IsEnabled = false;
        NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 149, 0)); // 橙色

        if (installPython && installUv)
        {
            NodeStatusText.Text = "正在安装 Python 和 uv...";
            NodeVersionText.Text = "这可能需要 3-5 分钟，请耐心等待";
        }
        else if (installPython)
        {
            NodeStatusText.Text = "正在安装 Python...";
            NodeVersionText.Text = "这可能需要 2-3 分钟，请耐心等待";
        }
        else
        {
            NodeStatusText.Text = "正在安装 uv...";
            NodeVersionText.Text = "这可能需要 1-2 分钟，请耐心等待";
        }
        NodeVersionText.Visibility = Visibility.Visible;

        // 构建安装脚本
        var commands = new List<string>();
        if (installPython)
        {
            // 使用 winget 安装 Python（国内速度快）
            commands.Add("$ProgressPreference = 'SilentlyContinue'; winget install --id Python.Python.3.11 -e --accept-source-agreements --accept-package-agreements");
        }
        if (installUv)
        {
            // 使用官方脚本安装 uv
            commands.Add("irm https://astral.sh/uv/install.ps1 | iex");
        }

        var combinedCommand = string.Join("; ", commands);

        var installProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{combinedCommand}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new System.Text.StringBuilder();
        var startTime = DateTime.Now;
        var dots = 0;
        var installStep = installPython ? "Python" : "uv";

        // 使用定时器更新显示
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        timer.Tick += (s, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                var elapsed = DateTime.Now - startTime;
                dots++;
                var dotsStr = new string('.', dots % 4);
                NodeStatusText.Text = $"正在安装 {installStep}{dotsStr} ({elapsed.TotalSeconds:F0}s)";
            });
        };
        timer.Start();

        installProcess.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    outputBuilder.AppendLine(e.Data);
                });
            }
        };

        installProcess.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    outputBuilder.AppendLine("[ERROR] " + e.Data);
                });
            }
        };

        installProcess.Start();
        installProcess.BeginOutputReadLine();
        installProcess.BeginErrorReadLine();

        // 在后台等待安装完成
        Task.Run(() =>
        {
            var completed = installProcess.WaitForExit(600000); // 10 分钟超时（Python 安装较慢）
            timer.Stop();

            Dispatcher.Invoke(() =>
            {
                if (completed && installProcess.ExitCode == 0)
                {
                    NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
                    NodeStatusText.Text = "✓ 安装成功！正在刷新环境...";
                    NodeVersionText.Text = "请等待几秒，然后点击「检测环境」验证";
                    InstallNodeButton.Content = "检测环境";
                    InstallNodeButton.IsEnabled = true;

                    // 自动刷新一次环境检测
                    Task.Delay(5000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(CheckNodeEnvironment);
                    });
                }
                else
                {
                    NodeStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                    NodeStatusText.Text = "✗ 安装失败，请手动安装";
                    var errorLog = outputBuilder.ToString();
                    if (errorLog.Contains(" cancelled") || errorLog.Contains("已取消"))
                    {
                        NodeVersionText.Text = "用户取消了安装";
                    }
                    else
                    {
                        NodeVersionText.Text = "请手动安装 Python 和 uv";
                    }
                    NodeVersionText.Visibility = Visibility.Visible;
                    InstallNodeButton.Content = "手动安装";
                    InstallNodeButton.IsEnabled = true;

                    // 打开下载页面
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "https://www.python.org/downloads/",
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        });
                    });
                }
            });
        });
    }
}
