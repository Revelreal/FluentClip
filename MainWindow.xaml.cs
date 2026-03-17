using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // Typing effect variables
    private System.Windows.Threading.DispatcherTimer? _typingTimer;
    private string _pendingResponse = "";
    private string _displayedResponse = "";
    private Border? _currentTypingBorder;
    private bool _isTyping;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _viewModel = new MainViewModel(_settings);
        _hotkeyManager = new HotkeyManager();
        DataContext = _viewModel;
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
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.RunInBackground)
        {
            e.Cancel = true;
            Hide();
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "请选择关闭方式：\n\n点击「是」：最小化到系统托盘（后台运行）\n点击「否」：完全退出程序",
            "关闭确认",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Hide();
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
            AgentColumn.Width = new GridLength(230);
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
            var settings = AgentSettings.Load();
            var avatarPath = settings.AvatarPath;
            
            if (string.IsNullOrEmpty(avatarPath) || avatarPath == "neko.png")
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
                    AgentAvatar.ImageSource = bitmap;
                }
            }
            else if (File.Exists(avatarPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(avatarPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                AgentAvatar.ImageSource = bitmap;
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

    private async void SendAgentMessage()
    {
        var userMessage = AgentInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        AddAgentMessage(userMessage, true);
        AgentInputTextBox.Text = "";

        var settings = AgentSettings.Load();
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            AddAgentMessage("请先在设置中配置API Key。", false);
            return;
        }

        var clipboardFilesInfo = "";
        var fileItems = _viewModel.ClipboardItems.Where(i => i.ItemType == ClipboardItemType.File).ToList();
        if (fileItems.Any())
        {
            clipboardFilesInfo = "\n\n【用户剪贴板中的文件】\n";
            foreach (var item in fileItems)
            {
                clipboardFilesInfo += $"- {item.FullPath}\n";
            }
            clipboardFilesInfo += "你可以使用read_file工具读取这些文件内容喵~\n";
        }

        var fullMessage = userMessage + clipboardFilesInfo;

        _agentService = new AgentService(settings);
        
        var lastAssistantMessage = AddAgentMessage("", false);
        var toolCallStatus = AddToolCallStatus("🔧 等待响应中...");
        
        string accumulatedResponse = "";

        // Initialize typing effect
        _pendingResponse = "";
        _displayedResponse = "";
        _isTyping = false;

        _agentService.OnToolCallStart += (toolName) =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateToolCallStatus(toolCallStatus, $"🔧 正在调用工具: {toolName}...");
            });
        };

        _agentService.OnStreamingResponse += (chunk) =>
        {
            Dispatcher.Invoke(() =>
            {
                accumulatedResponse += chunk;

                // If not currently typing, start the typing effect
                if (!_isTyping)
                {
                    _pendingResponse = accumulatedResponse;
                    _currentTypingBorder = lastAssistantMessage.Item1;
                    StartTypingEffect();
                }
                else
                {
                    // Add to pending response
                    _pendingResponse = accumulatedResponse;
                }
            });
        };

        _agentService.OnError += (error) =>
        {
            Dispatcher.Invoke(() =>
            {
                // Stop typing effect if active
                StopTypingEffect();
                RemoveToolCallStatus(toolCallStatus);
                UpdateLastAgentMessage(lastAssistantMessage.Item1, $"错误: {error}");
            });
        };

        try
        {
            await _agentService.SendMessageAsync(fullMessage);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StopTypingEffect();
                UpdateLastAgentMessage(lastAssistantMessage.Item1, $"错误: {ex.Message}");
            });
        }
    }

    private void StartTypingEffect()
    {
        _isTyping = true;
        _displayedResponse = "";

        // Create a timer for typing effect - 30ms per character for smooth animation
        _typingTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(25)
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

        // Display more characters (speed up by showing multiple chars at once)
        int charsToAdd = Math.Min(3, targetLength - currentLength);
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
            MaxWidth = 280
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
                item = new ClipboardItem
                {
                    ItemType = ClipboardItemType.File,
                    FilePaths = new[] { filePath },
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
                else
                {
                    _viewModel.CopyItemCommand.Execute(_pendingClickItem);
                }
            }
        }
        _pendingClickItem = null;
        _isDragging = false;
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
            var dataObject = new WpfDataObject();

            switch (item.ItemType)
            {
                case ClipboardItemType.Text:
                    if (!string.IsNullOrEmpty(item.TextContent))
                    {
                        dataObject.SetData(WpfDataFormats.Text, item.TextContent);
                    }
                    break;
                case ClipboardItemType.Image:
                    if (item.ImageContent != null)
                    {
                        dataObject.SetData(WpfDataFormats.Bitmap, item.ImageContent);
                    }
                    break;
                case ClipboardItemType.File:
                    if (item.FilePaths != null && item.FilePaths.Length > 0)
                    {
                        var files = new System.Collections.Specialized.StringCollection();
                        files.AddRange(item.FilePaths);
                        dataObject.SetFileDropList(files);
                    }
                    break;
            }

            var effects = _settings.DragActionCopy ? WpfDragDropEffects.Copy : WpfDragDropEffects.Copy | WpfDragDropEffects.Move;
            DragDrop.DoDragDrop(border, dataObject, effects);
        }
        finally
        {
            _isDragging = false;
        }
    }
}
