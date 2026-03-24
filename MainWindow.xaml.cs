using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FluentClip.Models;
using FluentClip.ViewModels;
using FluentClip.Services;
using Microsoft.Win32;
using System.Windows.Forms;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDataObject = System.Windows.DataObject;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace FluentClip;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly HotkeyManager _hotkeyManager;
    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isPinned;
    private DateTime _mouseDownTime;
    private ClipboardItem? _pendingClickItem;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _monitorMenuItem;
    private ToolStripMenuItem? _dragActionMenuItem;
    private bool _isAgentSidebarVisible;
    private AgentService? _agentService;
    private FileSystemWatcher? _aiWorkFolderWatcher;
    private readonly HashSet<string> _knownAiWorkFiles = new();

    // Typing effect variables
    private System.Windows.Threading.DispatcherTimer? _typingTimer;
    private string _pendingResponse = "";
    private string _displayedResponse = "";
    private string _streamingBuffer = "";
    private Border? _currentTypingBorder;
    private bool _isTyping;
    private bool _isToolCallInProgress = false;

    private readonly Random _tipRandom = new();
    private readonly HashSet<int> _shownTipIndices = new();
    private readonly List<string> _tips = new()
    {
        "💡 嘿，你知道吗？可以让我帮你读取文件内容哦~",
        "💡 嘿，你知道吗？直接问我「列出目录」可以查看文件夹内容~",
        "💡 嘿，你知道吗？问我天气可以查询任意城市的气温~",
        "💡 嘿，你知道吗？复制文件后我可以帮你分析文件类型~",
        "💡 嘿，你知道吗？可以让我执行Shell命令来帮你做事~",
        "💡 嘿，你知道吗？「搜索+关键词」可以让我帮你上网查找信息~",
        "💡 嘿，你知道吗？剪贴板暂存区可以帮你管理多个复制内容~",
        "💡 嘿，你知道吗？AI工作文件夹生成的文件会自动进入暂存区~",
        "💡 嘿，你知道吗？连续多次复制文件我会跟你聊聊天哦~",
        "💡 嘿，你知道吗？把文件拖到悬浮窗上可以快速添加到暂存区~",
        "💡 嘿，你知道吗？可以让我帮你写代码、生成文案~",
        "💡 嘿，你知道吗？「读取+文件路径」可以查看文件内容~",
        "💡 嘿，你知道吗？问我「有什么功能」可以了解我能做什么~"
    };

    private void ShowRandomTip()
    {
        if (_shownTipIndices.Count >= _tips.Count)
        {
            _shownTipIndices.Clear();
        }

        if (_tipRandom.Next(100) < 40)
        {
            var availableIndices = Enumerable.Range(0, _tips.Count).Where(i => !_shownTipIndices.Contains(i)).ToList();
            if (availableIndices.Count == 0)
            {
                _shownTipIndices.Clear();
                availableIndices = Enumerable.Range(0, _tips.Count).ToList();
            }
            
            int tipIndex = availableIndices[_tipRandom.Next(availableIndices.Count)];
            _shownTipIndices.Add(tipIndex);
            string tip = _tips[tipIndex];
            
            var tipBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 252)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = tip,
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 142, 147)),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                FontStyle = System.Windows.FontStyles.Italic,
                Opacity = 0.7
            };

            tipBorder.Child = tipText;
            AgentChatPanel.Children.Add(tipBorder);
            AgentChatScrollViewer.ScrollToEnd();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _viewModel = new MainViewModel(_settings);
        _hotkeyManager = new HotkeyManager();
        DataContext = _viewModel;
        
        var emojiFontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial");
        ToastText.FontFamily = emojiFontFamily;
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CreateNotifyIcon();
        RegisterHotkeys();

        if (_settings.AutoStart)
        {
            SetAutoStart(true);
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_settings.MonitorClipboard)
            {
                _viewModel.StartMonitoring(this);
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        PinButton.Content = _isPinned ? "📍" : "📌";
        
        InitializeAiWorkFolderWatcher();
    }
    
    private void InitializeAiWorkFolderWatcher()
    {
        var settings = AgentSettings.Load();
        if (string.IsNullOrEmpty(settings.AiWorkFolder))
        {
            return;
        }
        
        if (!Directory.Exists(settings.AiWorkFolder))
        {
            return;
        }
        
        try
        {
            _aiWorkFolderWatcher = new FileSystemWatcher(settings.AiWorkFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            
            _aiWorkFolderWatcher.Created += OnAiWorkFolderFileCreated;
            _aiWorkFolderWatcher.Changed += OnAiWorkFolderFileChanged;
            
            foreach (var file in Directory.GetFiles(settings.AiWorkFolder))
            {
                _knownAiWorkFiles.Add(file);
            }
            
            Log($"[DEBUG] AI工作文件夹监控已启动: {settings.AiWorkFolder}");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 初始化AI工作文件夹监控失败: {ex.Message}");
        }
    }
    
    private void OnAiWorkFolderFileCreated(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.FullPath)) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!_knownAiWorkFiles.Contains(e.FullPath))
                {
                    _knownAiWorkFiles.Add(e.FullPath);
                    
                    var ext = Path.GetExtension(e.FullPath)?.ToLowerInvariant();
                    if (IsValidFileType(ext))
                    {
                        AddFileToList(e.FullPath);
                        ShowToast($"📁 AI生成了新文件: {Path.GetFileName(e.FullPath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] 处理AI工作文件夹新文件失败: {ex.Message}");
            }
        });
    }
    
    private void OnAiWorkFolderFileChanged(object sender, FileSystemEventArgs e)
    {
    }
    
    private bool IsValidFileType(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return false;
        
        var validExtensions = new[] 
        { 
            ".txt", ".md", ".json", ".xml", ".cs", ".js", ".ts", ".tsx", ".jsx",
            ".html", ".css", ".py", ".java", ".c", ".cpp", ".h", ".hpp", ".go",
            ".rs", ".rb", ".php", ".sql", ".yaml", ".yml", ".ini", ".cfg",
            ".log", ".bat", ".ps1", ".sh", ".pdf", ".doc", ".docx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico",
            ".zip", ".rar", ".7z", ".tar", ".gz"
        };
        
        return validExtensions.Contains(ext);
    }

    private static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return null;

        int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && child is FrameworkElement fe && fe.Name == childName)
            {
                return typedChild;
            }

            var foundChild = FindChild<T>(child, childName);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }

    private void Log(string message)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
        catch { }
    }

    private void RegisterHotkeys()
    {
        _hotkeyManager.Start(this);
        
        _hotkeyManager.RegisterHotkey(_settings.ShowHotkey, () =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                if (IsVisible)
                {
                    Activate();
                }
                else
                {
                    ShowWindow();
                }
            });
        });
        
        _hotkeyManager.RegisterHotkey(_settings.SettingsHotkey, () =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                ShowWindow();
                SettingsButton_Click(this, new RoutedEventArgs());
            });
        });
        
        _hotkeyManager.RegisterHotkey(_settings.PinHotkey, () =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                PinButton_Click(this, new RoutedEventArgs());
            });
        });
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Icon = new System.Drawing.Icon(WpfApplication.GetResourceStream(
            new Uri("pack://application:,,,/clip.ico")).Stream);
        _notifyIcon.Text = "FluentClip";
        _notifyIcon.Visible = true;
        
        var contextMenu = new ContextMenuStrip();
        
        contextMenu.Items.Add("打开", null, (s, e) => ShowWindow());
        contextMenu.Items.Add("-");
        
        _monitorMenuItem = new ToolStripMenuItem("监控剪贴板");
        _monitorMenuItem.Checked = _settings.MonitorClipboard;
        _monitorMenuItem.Click += (s, e) =>
        {
            _settings.MonitorClipboard = !_settings.MonitorClipboard;
            _monitorMenuItem.Checked = _settings.MonitorClipboard;
            _settings.Save();
            
            if (_settings.MonitorClipboard)
            {
                _viewModel.StartMonitoring(this);
            }
            else
            {
                _viewModel.StopMonitoring();
            }
        };
        contextMenu.Items.Add(_monitorMenuItem);
        
        _dragActionMenuItem = new ToolStripMenuItem("拖入文件行为");
        var copyItem = new ToolStripMenuItem("仅保存副本（复制）");
        var cutItem = new ToolStripMenuItem("剪切原文件");
        copyItem.Click += (s, e) =>
        {
            _settings.DragActionCopy = true;
            _dragActionMenuItem.DropDownItems[0].Text = "✓ 仅保存副本（复制）";
            _dragActionMenuItem.DropDownItems[1].Text = "剪切原文件";
            _settings.Save();
        };
        cutItem.Click += (s, e) =>
        {
            _settings.DragActionCopy = false;
            _dragActionMenuItem.DropDownItems[0].Text = "仅保存副本（复制）";
            _dragActionMenuItem.DropDownItems[1].Text = "✓ 剪切原文件";
            _settings.Save();
        };
        
        if (_settings.DragActionCopy)
        {
            copyItem.Text = "✓ 仅保存副本（复制）";
        }
        else
        {
            cutItem.Text = "✓ 剪切原文件";
        }
        
        _dragActionMenuItem.DropDownItems.Add(copyItem);
        _dragActionMenuItem.DropDownItems.Add(cutItem);
        contextMenu.Items.Add(_dragActionMenuItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        var pinMenuItem = new ToolStripMenuItem(_isPinned ? "取消置顶" : "置顶");
        pinMenuItem.Click += (s, e) =>
        {
            PinButton_Click(this, new RoutedEventArgs());
            pinMenuItem.Text = _isPinned ? "取消置顶" : "置顶";
        };
        contextMenu.Items.Add(pinMenuItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (s, e) => ForceClose());
        _notifyIcon.ContextMenuStrip = contextMenu;
        
        _notifyIcon.DoubleClick += (s, e) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            Hide();
            ShowTrayWindow();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.RunInBackground)
        {
            e.Cancel = true;
            Hide();
            ShowTrayWindow();
        }
        else
        {
            _viewModel.Dispose();
        }
    }

    public void ForceClose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _hotkeyManager.Dispose();
        _viewModel.Dispose();
        WpfApplication.Current.Shutdown();
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("FluentClip", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("FluentClip", false);
                }
                key.Close();
            }
        }
        catch { }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 忽略DragMove异常，当不在鼠标按下状态时调用会抛出异常
            }
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        
        PinButton.Content = _isPinned ? "📍" : "📌";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private TrayWindow? _trayWindow;

    private void ShowTrayWindow()
    {
        try
        {
            if (_trayWindow == null)
            {
                _trayWindow = new TrayWindow();
                _trayWindow.SetMainWindow(this);
                _trayWindow.SetMainWindowTop(this.Top);
                _trayWindow.SetMainWindowLeft(this.Left);
                _trayWindow.StartClipboardMonitoring();
            }
            _trayWindow.ShowWindow();
            System.Diagnostics.Debug.WriteLine("TrayWindow shown successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing TrayWindow: {ex.Message}");
            System.Windows.MessageBox.Show($"Error showing tray window: {ex.Message}");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "请选择关闭方式：\n\n点击「是」：最小化到悬浮窗（后台运行）\n点击「否」：完全退出程序",
            "关闭确认",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Hide();
            ShowTrayWindow();
        }
        else if (result == System.Windows.MessageBoxResult.No)
        {
            ForceClose();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(this, _settings);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void AgentButton_Click(object sender, RoutedEventArgs e)
    {
        _isAgentSidebarVisible = !_isAgentSidebarVisible;
        
        if (_isAgentSidebarVisible)
        {
            AgentSidebar.Visibility = Visibility.Visible;
            AgentColumn.Width = new GridLength(300);
            AgentColumn.MinWidth = 230;
            MinWidth = 650;
            MinHeight = 560;
            Width = 700;
            LoadAgentAvatar();
        }
        else
        {
            AgentColumn.Width = new GridLength(0);
            AgentColumn.MinWidth = 0;
            AgentSidebar.Visibility = Visibility.Collapsed;
            MinWidth = 425;
            MinHeight = 560;
            Width = 450;
        }
    }

    private void LoadAgentAvatar()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var nekoPath = Path.Combine(exeDir, "neko.png");
            
            if (File.Exists(nekoPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(nekoPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                AgentAvatarImage.Source = bitmap;
            }
            else
            {
                var settings = AgentSettings.Load();
                var avatarPath = settings.AvatarPath;
                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AgentAvatarImage.Source = bitmap;
                }
            }
        }
        catch { }
    }

    private void AgentTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 忽略DragMove异常，当不在鼠标按下状态时调用会抛出异常
            }
        }
    }

    private void AgentSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var agentSettingsWindow = new AgentSettingsWindow(this);
        agentSettingsWindow.Owner = this;
        agentSettingsWindow.ShowDialog();
    }

    private void AgentInputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(AgentInputTextBox.Text))
        {
            SendAgentMessage();
        }
    }

    private void AgentSendButton_Click(object sender, RoutedEventArgs e)
    {
        SendAgentMessage();
    }

    private void AgentCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_agentService != null && _agentService.IsProcessing)
        {
            _agentService.Cancel();
            AddAgentMessage("⚠️ 对话已取消", false);
            HideAgentCancelButton();
        }
    }

    private void ShowAgentCancelButton()
    {
        Dispatcher.Invoke(() =>
        {
            AgentCancelButton.Visibility = Visibility.Visible;
            AgentSendButton.Visibility = Visibility.Collapsed;
        });
    }

    private void HideAgentCancelButton()
    {
        Dispatcher.Invoke(() =>
        {
            AgentCancelButton.Visibility = Visibility.Collapsed;
            AgentSendButton.Visibility = Visibility.Visible;
        });
    }

    private async void SendAgentMessage()
    {
        var userMessage = AgentInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        StopTypingEffect();

        AddAgentMessage(userMessage, true);
        AgentInputTextBox.Text = "";
        ShowAgentCancelButton();

        var settings = AgentSettings.Load();
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            AddAgentMessage("请先在设置中配置API Key。", false);
            return;
        }

        if (_agentService == null)
        {
            _agentService = new AgentService(settings);
            _agentService.GetStagingFilesCallback = () => _viewModel.ClipboardItems;
            _agentService.GetAiWorkFolderFilesCallback = () => GetAiWorkFolderFiles();
            _agentService.OnContextSummarized += HandleContextSummarized;
            _agentService.OnConfirmHighRiskCommand += HandleConfirmHighRiskCommand;
            _agentService.OnShellOutput += HandleShellOutput;
            _agentService.OnShellError += HandleShellError;
            _agentService.OnShellComplete += HandleShellComplete;
        }

        UpdateTokenUsageDisplay();

        if (_agentService.ShouldSummarize)
        {
            AddAgentMessage("上下文即将达到上限，正在自动总结...", false);
            var summary = await _agentService.SummarizeContextAsync();
            AddAgentMessage($"✅ 上下文已总结，新token使用: {_agentService.TotalTokens} / {_agentService.MaxTokenLimit} ({_agentService.TokenUsagePercentage:F1}%)", false);
        }
        else if (_agentService.CheckAndClearContextIfFull())
        {
            AddAgentMessage("上下文已自动清除，开始新对话~ 🐾", false);
        }

        var messageBorder = AddAgentMessage("", false);
        var statusBorder = AddToolCallStatus("🔧 等待响应中...");

        string currentResponse = "";

        _pendingResponse = "";
        _displayedResponse = "";
        _isTyping = false;

        void HandleStreaming(string chunk)
        {
            Dispatcher.Invoke(() =>
            {
                currentResponse += chunk;
                _pendingResponse = currentResponse;

                var targetBorder = messageBorder.Item1;
                _currentTypingBorder = targetBorder;

                if (!_isTyping)
                {
                    _displayedResponse = "";
                    StartTypingEffect();
                }

                AgentChatScrollViewer.ScrollToEnd();
            });
        }

        void HandleToolCallStart(string toolName)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateToolCallStatus(statusBorder, $"🔧 正在调用工具: {toolName}...");
            });
        }

        void HandleError(string error)
        {
            Dispatcher.Invoke(() =>
            {
                StopTypingEffect();
                RemoveToolCallStatus(statusBorder);
                UpdateLastAgentMessage(messageBorder.Item1, $"错误: {error}");
                HideAgentCancelButton();
            });
        }

        void HandleComplete()
        {
            Dispatcher.Invoke(() =>
            {
                StopTypingEffect();
                RemoveToolCallStatus(statusBorder);
                UpdateTokenUsageDisplay();
                ShowRandomTip();
                HideAgentCancelButton();
            });
        }

        _agentService.OnStreamingResponse += HandleStreaming;
        _agentService.OnToolCallStart += HandleToolCallStart;
        _agentService.OnError += HandleError;
        _agentService.OnComplete += HandleComplete;

        try
        {
            await _agentService.SendMessageAsync(userMessage);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StopTypingEffect();
                RemoveToolCallStatus(statusBorder);
                UpdateLastAgentMessage(messageBorder.Item1, $"错误: {ex.Message}");
            });
        }
    }

    private async Task<bool> HandleConfirmHighRiskCommand(string command)
    {
        var result = System.Windows.MessageBox.Show(
            $"⚠️ AI请求执行以下高风险命令：\n\n{command}\n\n是否允许执行？",
            "高风险命令确认",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return result == System.Windows.MessageBoxResult.Yes;
    }

    private Border? _shellOutputBorder;
    private TextBlock? _shellOutputTextBlock;
    private bool _isFirstShellOutput = true;

    private void HandleShellOutput(string output)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_shellOutputBorder == null || _shellOutputTextBlock == null)
            {
                var result = CreateShellOutputPanel();
                _shellOutputBorder = result.border;
                _shellOutputTextBlock = result.textBlock;
            }

            if (_isFirstShellOutput && AgentChatPanel.Children.Count > 0)
            {
                var lastChild = AgentChatPanel.Children[AgentChatPanel.Children.Count - 1];
                if (lastChild is Border lastBorder)
                {
                    var lastContent = FindChild<TextBlock>(lastBorder, "AgentMessageText");
                    if (lastContent != null && string.IsNullOrWhiteSpace(lastContent.Text))
                    {
                        AgentChatPanel.Children.Remove(lastBorder);
                    }
                }
                _isFirstShellOutput = false;
            }

            _shellOutputTextBlock.Text += output;
            AgentChatScrollViewer.ScrollToEnd();
        });
    }

    private void HandleShellError(string error)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_shellOutputBorder != null && _shellOutputTextBlock != null)
            {
                _shellOutputTextBlock.Text += $"\n❌ {error}";
                AgentChatScrollViewer.ScrollToEnd();
            }
        });
    }

    private void HandleShellComplete()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _isFirstShellOutput = true;
            _shellOutputBorder = null;
            _shellOutputTextBlock = null;
        });
    }

    private (Border border, TextBlock textBlock) CreateShellOutputPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 28)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            MaxWidth = AgentChatPanel.ActualWidth > 0 ? AgentChatPanel.ActualWidth - 40 : 400,
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
            BorderThickness = new Thickness(1)
        };

        var textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254)),
            TextWrapping = TextWrapping.Wrap,
            Text = ""
        };

        border.Child = textBlock;
        AgentChatPanel.Children.Add(border);

        return (border, textBlock);
    }

    private void RebuildChatDisplay()
    {
        if (_agentService == null || AgentChatPanel == null) return;
        
        AgentChatPanel.Children.Clear();
        
        var history = _agentService.GetConversationHistory();
        if (history == null) return;
        
        foreach (var message in history)
        {
            if (message == null || string.IsNullOrEmpty(message.Content)) continue;
            
            if (message.Role == "user")
            {
                AddAgentMessage(message.Content, true);
            }
            else if (message.Role == "assistant")
            {
                AddAgentMessage(message.Content, false);
            }
        }
    }

    private IEnumerable<string> GetAiWorkFolderFiles()
    {
        var settings = AgentSettings.Load();
        if (string.IsNullOrEmpty(settings.AiWorkFolder))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            if (Directory.Exists(settings.AiWorkFolder))
            {
                return Directory.GetFiles(settings.AiWorkFolder);
            }
        }
        catch { }

        return Enumerable.Empty<string>();
    }

    private void UpdateTokenUsageDisplay()
    {
        if (_agentService == null) return;

        Dispatcher.Invoke(() =>
        {
            var totalTokens = _agentService.TotalTokens;
            var maxTokens = _agentService.MaxTokenLimit;
            var percentage = _agentService.TokenUsagePercentage;

            TokenUsageText.Text = $"上下文: {totalTokens:N0} / {maxTokens:N0}";
            TokenPercentageText.Text = $" ({percentage:F1}%)";

            if (percentage >= 90)
            {
                TokenPercentageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            }
            else if (percentage >= 70)
            {
                TokenPercentageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0));
            }
            else
            {
                TokenPercentageText.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            }
        });
    }

    private void HandleContextSummarized(string summary)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateTokenUsageDisplay();
        });
    }

    private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_agentService == null)
        {
            var settings = AgentSettings.Load();
            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                ShowToast("请先在设置中配置API Key");
                return;
            }
            _agentService = new AgentService(settings);
            _agentService.GetStagingFilesCallback = () => _viewModel.ClipboardItems;
            _agentService.GetAiWorkFolderFilesCallback = () => GetAiWorkFolderFiles();
            _agentService.OnContextSummarized += HandleContextSummarized;
            _agentService.OnConfirmHighRiskCommand += HandleConfirmHighRiskCommand;
            _agentService.OnShellOutput += HandleShellOutput;
            _agentService.OnShellError += HandleShellError;
            _agentService.OnShellComplete += HandleShellComplete;
        }

        if (_agentService.MessageCount > 0)
        {
            RebuildChatDisplay();
        }

        if (_agentService.MessageCount == 0)
        {
            ShowToast("没有上下文可以总结喵~");
            return;
        }

        AddAgentMessage("正在手动触发上下文总结...", false);
        var summary = await _agentService.SummarizeContextAsync();
        AddAgentMessage($"✅ 总结完成！\n{summary}", false);
        UpdateTokenUsageDisplay();
    }

    private void StartTypingEffect()
    {
        _isTyping = true;
        _displayedResponse = "";

        _typingTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _typingTimer.Tick += TypingTimer_Tick;
        _typingTimer.Start();
    }

    private void StopTypingEffect()
    {
        if (_typingTimer != null)
        {
            _typingTimer.Stop();
            _typingTimer.Tick -= TypingTimer_Tick;
            _typingTimer = null;
        }

        _isTyping = false;
        
        // Display the full response
        if (_currentTypingBorder != null && !string.IsNullOrEmpty(_pendingResponse))
        {
            UpdateLastAgentMessage(_currentTypingBorder, _pendingResponse);
        }

        _pendingResponse = "";
        _displayedResponse = "";
    }

    private void TypingTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentTypingBorder == null) return;

        // Calculate how many characters to display
        int targetLength = _pendingResponse.Length;
        int currentLength = _displayedResponse.Length;

        if (currentLength >= targetLength)
        {
            // Finished typing, render markdown
            StopTypingEffect();
            return;
        }

        int charsToAdd = Math.Min(5, targetLength - currentLength);
        _displayedResponse = _pendingResponse.Substring(0, currentLength + charsToAdd);

        // Update the display
        UpdateLastAgentMessage(_currentTypingBorder, _displayedResponse);
    }

    private (Border, string) AddAgentMessage(string text, bool isUser)
    {
        var messageWrapper = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var border = new Border
        {
            Background = isUser
                ? new SolidColorBrush(Color.FromRgb(0, 122, 255))
                : new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 10, 14, 10),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("CardShadow"),
            MaxWidth = AgentChatPanel.ActualWidth > 0 ? AgentChatPanel.ActualWidth - 60 : 500
        };

        FrameworkElement content;

        if (isUser)
        {
            var textBox = new System.Windows.Controls.TextBox
            {
                Text = text,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("-apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Segoe UI', sans-serif"),
                SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255))
            };
            content = textBox;
        }
        else
        {
            var panel = MarkdownRenderer.Render(text);
            
            if (panel is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is System.Windows.Controls.TextBox tb)
                    {
                        tb.IsReadOnly = true;
                    }
                    else if (child is Border b && b.Child is System.Windows.Controls.TextBlock childTb)
                    {
                        var wrapper = new System.Windows.Controls.TextBox
                        {
                            Text = childTb.Text,
                            FontSize = childTb.FontSize,
                            Foreground = childTb.Foreground,
                            Background = Brushes.Transparent,
                            IsReadOnly = true,
                            BorderThickness = new Thickness(0),
                            TextWrapping = childTb.TextWrapping,
                            FontFamily = childTb.FontFamily
                        };
                        b.Child = wrapper;
                    }
                }
            }
            
            var scrollViewer = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 400
            };
            content = scrollViewer;
        }

        if (isUser)
        {
            border.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            border.Margin = new Thickness(50, 0, 0, 0);
        }
        else
        {
            border.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            border.Margin = new Thickness(0, 0, 40, 0);
        }

        border.Child = content;
        messageWrapper.Children.Add(border);
        AgentChatPanel.Children.Add(messageWrapper);
        
        AgentChatScrollViewer.ScrollToEnd();
        
        return (border, text);
    }

    private void UpdateLastAgentMessage(Border border, string text)
    {
        var targetBorder = border;
        if (border.Parent is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is Border firstChild)
        {
            targetBorder = firstChild;
        }

        if (_isTyping)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 61, 61)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = 20
            };
            targetBorder.Child = textBlock;
        }
        else
        {
            targetBorder.Child = MarkdownRenderer.RenderWithThinking(text);
        }
        AgentChatScrollViewer.ScrollToEnd();
    }

    private Border AddToolCallStatus(string text)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 248, 230)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Tag = text
        };

        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 140, 0))
        };

        border.Child = textBlock;
        AgentChatPanel.Children.Add(border);
        AgentChatScrollViewer.ScrollToEnd();
        
        return border;
    }

    private void UpdateToolCallStatus(Border border, string text)
    {
        if (border.Child is TextBlock textBlock)
        {
            textBlock.Text = text;
        }
        AgentChatScrollViewer.ScrollToEnd();
    }

    private void RemoveToolCallStatus(Border border)
    {
        AgentChatPanel.Children.Remove(border);
    }

    public ViewModels.MainViewModel? GetViewModel()
    {
        return _viewModel;
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Multiselect = true,
            Filter = "所有文件|*.*|图片|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|文本|*.txt;*.md;*.json;*.xml"
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            foreach (var file in dialog.FileNames)
            {
                AddFileToList(file);
            }
        }
    }

    private void AddFileToList(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLower();
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var textExts = new[] { ".txt", ".md", ".json", ".xml", ".cs", ".js", ".html", ".css" };

            ClipboardItem item;
            BitmapSource? thumbnail = null;

            if (imageExts.Contains(ext))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.Image,
                    ImageContent = bitmap,
                    Thumbnail = bitmap,
                    FilePaths = new[] { filePath },
                    Timestamp = DateTime.Now
                };
            }
            else if (textExts.Contains(ext))
            {
                var text = File.ReadAllText(filePath);
                item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.Text,
                    TextContent = text,
                    FilePaths = new[] { filePath },
                    Timestamp = DateTime.Now
                };
            }
            else
            {
                thumbnail = ThumbnailHelper.GenerateThumbnail(filePath, 96, 96);
                
                item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.File,
                    FilePaths = new[] { filePath },
                    Thumbnail = thumbnail,
                    Timestamp = DateTime.Now
                };
            }

            _viewModel.ClipboardItems.Insert(0, item);
        }
        catch
        {
        }
    }

    private void DropZone_DragEnter(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop) || e.Data.GetDataPresent(WpfDataFormats.Bitmap) || e.Data.GetDataPresent(WpfDataFormats.Text))
        {
            e.Effects = WpfDragDropEffects.Copy;
        }
        else
        {
            e.Effects = WpfDragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, WpfDragEventArgs e)
    {
    }

    private void DropZone_Drop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(WpfDataFormats.FileDrop)!;
            if (files != null && files.Length > 0)
            {
                foreach (var file in files)
                {
                    AddFileToList(file);
                }
            }
        }
        else if (e.Data.GetDataPresent(WpfDataFormats.Bitmap))
        {
            var bitmap = e.Data.GetData(WpfDataFormats.Bitmap) as BitmapSource;
            if (bitmap != null)
            {
                var item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.Image,
                    ImageContent = bitmap,
                    Timestamp = DateTime.Now
                };
                _viewModel.ClipboardItems.Insert(0, item);
            }
        }
        else if (e.Data.GetDataPresent(WpfDataFormats.Text))
        {
            var text = e.Data.GetData(WpfDataFormats.Text) as string;
            if (!string.IsNullOrEmpty(text))
            {
                var item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.Text,
                    TextContent = text,
                    Timestamp = DateTime.Now
                };
                _viewModel.ClipboardItems.Insert(0, item);
            }
        }
    }

    private void ItemCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ClipboardItem item)
        {
            _dragStartPoint = e.GetPosition(this);
            _mouseDownTime = DateTime.Now;
            _pendingClickItem = item;
            _isDragging = false;
        }
    }

    private void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_pendingClickItem != null && !_isDragging)
        {
            var elapsed = DateTime.Now - _mouseDownTime;
            if (elapsed.TotalMilliseconds < 300)
            {
                if (_pendingClickItem.ItemType == ClipboardItemType.File && _pendingClickItem.FilePaths?.Length > 0)
                {
                    var filePath = _pendingClickItem.FilePaths[0];
                    if (System.IO.File.Exists(filePath) || System.IO.Directory.Exists(filePath))
                    {
                        if (System.IO.File.Exists(filePath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                        }
                        else if (System.IO.Directory.Exists(filePath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"\"{filePath}\"");
                        }
                    }
                }
                else if (_pendingClickItem.ItemType == ClipboardItemType.Text && !string.IsNullOrEmpty(_pendingClickItem.TextContent))
                {
                    CopyTextToClipboardDirectly(_pendingClickItem.TextContent);
                    ShowCopySuccessToast();
                }
                else if (_pendingClickItem.ItemType == ClipboardItemType.Image && _pendingClickItem.ImageContent != null)
                {
                    if (_pendingClickItem.FilePaths?.Length > 0 && System.IO.File.Exists(_pendingClickItem.FilePaths[0]))
                    {
                        var filePath = _pendingClickItem.FilePaths[0];
                        var directory = System.IO.Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                        }
                    }
                    else
                    {
                        ShowToast("⚠️ 未能找到文件路径，等待兼容性修复");
                    }
                }
                else
                {
                    _viewModel.CopyItemCommand.Execute(_pendingClickItem);
                }
            }
        }
        _pendingClickItem = null;
        _isDragging = false;
    }

    private void CopyTextToClipboardDirectly(string text)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                break;
            }
            catch
            {
                System.Threading.Thread.Sleep(50);
            }
        }
    }

    private void ItemCard_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is Border border && border.Tag is ClipboardItem item)
        {
            var currentPoint = e.GetPosition(this);
            if (!_isDragging && (Math.Abs(currentPoint.X - _dragStartPoint.X) > 5 || Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 5))
            {
                _isDragging = true;
                StartDrag(border, item);
            }
        }
        else
        {
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private void StartDrag(Border border, ClipboardItem item)
    {
        try
        {
            object dataObject;

            switch (item.ItemType)
            {
                case ClipboardItemType.Text:
                    dataObject = new WpfDataObject();
                    if (!string.IsNullOrEmpty(item.TextContent))
                    {
                        ((WpfDataObject)dataObject).SetData(WpfDataFormats.Text, item.TextContent);
                    }
                    break;
                case ClipboardItemType.Image:
                    dataObject = new WpfDataObject();
                    if (item.ImageContent != null)
                    {
                        ((WpfDataObject)dataObject).SetData(WpfDataFormats.Bitmap, item.ImageContent);
                    }
                    break;
                case ClipboardItemType.File:
                    if (item.FilePaths != null && item.FilePaths.Length > 0)
                    {
                        dataObject = CreateFileDataObject(item.FilePaths);
                    }
                    else
                    {
                        dataObject = new WpfDataObject();
                    }
                    break;
                default:
                    dataObject = new WpfDataObject();
                    break;
            }

            var effects = _settings.DragActionCopy ? WpfDragDropEffects.Copy : WpfDragDropEffects.Copy | WpfDragDropEffects.Move;
            
            if (dataObject is System.Windows.IDataObject wpfDataObject)
            {
                DragDrop.DoDragDrop(border, wpfDataObject, effects);
            }
            else if (dataObject is System.Windows.Forms.DataObject winFormsDataObject)
            {
                DragDrop.DoDragDrop(border, new DataObjectAdapter(winFormsDataObject), effects);
            }
            else
            {
                DragDrop.DoDragDrop(border, new WpfDataObject(), effects);
            }
        }
        finally
        {
            _isDragging = false;
        }
    }

    private object CreateFileDataObject(string[] filePaths)
    {
        try
        {
            var winFormsDataObject = new System.Windows.Forms.DataObject();
            
            var files = new System.Collections.Specialized.StringCollection();
            files.AddRange(filePaths);
            winFormsDataObject.SetFileDropList(files);
            
            var fileNames = new System.Collections.Specialized.StringCollection();
            foreach (var path in filePaths)
            {
                fileNames.Add(Path.GetFileName(path));
            }
            try
            {
                winFormsDataObject.SetData("FileNameW", fileNames);
            }
            catch { }

            try
            {
                var shellDataObject = ShellDragDropHelper.CreateDataObjectForDrag(filePaths);
                if (shellDataObject != null)
                {
                    return shellDataObject;
                }
            }
            catch { }

            return winFormsDataObject;
        }
        catch
        {
            var files = new System.Collections.Specialized.StringCollection();
            files.AddRange(filePaths);
            var dataObject = new System.Windows.Forms.DataObject();
            dataObject.SetFileDropList(files);
            return dataObject;
        }
    }

    private static readonly string[] CopySuccessMessages = new[]
    {
        "复制成功~nya! ✨",
        "搞定啦~喵~ 🐱",
        "复制完成! ✓",
        "已经复制好了哟~ 😊",
        "好耶！复制成功~ 🎉",
        "完成啦~给你小心心 ♥",
        "复制完毕~继续加油! 💪",
        "成功复制!赞! 👍",
        "复制好啦~ 🌟",
        "超棒！复制完成~ ✨"
    };

    private void ShowCopySuccessToast()
    {
        var message = CopySuccessMessages[new Random().Next(CopySuccessMessages.Length)];
        ShowToast(message);
    }

    private void ShowToast(string message)
    {
        var emojiFont = new System.Windows.Media.FontFamily("Segoe UI Emoji");
        var normalFont = new System.Windows.Media.FontFamily("Segoe UI, -apple-system, BlinkMacSystemFont, sans-serif");
        
        ToastText.Inlines.Clear();
        
        string[] emojis = { "✨", "🐱", "✓", "😊", "🎉", "♥", "💪", "👍", "🌟" };
        int emojiStart = -1;
        foreach (var emoji in emojis)
        {
            int idx = message.LastIndexOf(emoji);
            if (idx >= 0 && (emojiStart < 0 || idx > emojiStart))
            {
                emojiStart = idx;
            }
        }
        
        if (emojiStart >= 0)
        {
            if (emojiStart > 0)
            {
                ToastText.Inlines.Add(new Run(message.Substring(0, emojiStart)) { FontFamily = normalFont });
            }
            ToastText.Inlines.Add(new Run(message.Substring(emojiStart)) { FontFamily = emojiFont });
        }
        else
        {
            ToastText.Inlines.Add(new Run(message) { FontFamily = normalFont });
        }
        
        ToastPopup.BeginAnimation(OpacityProperty, null);
        ToastPopup.Opacity = 1;

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.BeginTime = TimeSpan.FromMilliseconds(1800);
        
        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, ToastPopup);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        storyboard.Begin();
    }

    internal class DataObjectAdapter : System.Windows.IDataObject
    {
        private readonly System.Windows.Forms.DataObject _winFormsDataObject;

        public DataObjectAdapter(System.Windows.Forms.DataObject winFormsDataObject)
        {
            _winFormsDataObject = winFormsDataObject;
        }

        public object GetData(string format, bool autoConvert)
        {
            return _winFormsDataObject.GetData(format, autoConvert);
        }

        public object GetData(string format)
        {
            return _winFormsDataObject.GetData(format);
        }

        public object GetData(Type format)
        {
            return _winFormsDataObject.GetData(format);
        }

        public void SetData(string format, object data, bool autoConvert)
        {
            _winFormsDataObject.SetData(format, data);
        }

        public void SetData(string format, object data)
        {
            _winFormsDataObject.SetData(format, data);
        }

        public void SetData(Type format, object data)
        {
            _winFormsDataObject.SetData(format, data);
        }

        public void SetData(object data)
        {
            _winFormsDataObject.SetData(data);
        }

        public bool GetDataPresent(string format, bool autoConvert)
        {
            return _winFormsDataObject.GetDataPresent(format, autoConvert);
        }

        public bool GetDataPresent(string format)
        {
            return _winFormsDataObject.GetDataPresent(format);
        }

        public bool GetDataPresent(Type format)
        {
            return _winFormsDataObject.GetDataPresent(format);
        }

        public string[] GetFormats(bool autoConvert)
        {
            return _winFormsDataObject.GetFormats(autoConvert);
        }

        public string[] GetFormats()
        {
            return _winFormsDataObject.GetFormats();
        }
    }
}
