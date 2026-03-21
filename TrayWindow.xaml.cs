using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FluentClip.Services;

namespace FluentClip
{
    public partial class TrayWindow : Window
    {
        private static TrayWindow? _instance;
        private MainWindow? _mainWindow;
        private Point _dragStartPoint;
        private bool _isDragging;
        
        private double _velocityX = 0;
        private double _rotationAngle = 0;
        private double _rotationTime = 0;
        private const double RotationSpeed = 0.15;
        private const double MaxRotationAngle = 15;
        private bool _isFalling = false;
        private bool _isAttached = false;
        private BitmapImage? _grabImage;
        private BitmapImage? _attachImage;
        private DispatcherTimer? _physicsTimer;
        private ClipboardService? _clipboardService;
        private string? _lastClipboardContent;

        public TrayWindow(int size = 200)
        {
            InitializeComponent();
            _instance = this;
            
            Width = size;
            Height = (int)(size * 1.333);
            
            System.Diagnostics.Debug.WriteLine($"TrayWindow constructor called, size: {size}x{Height}");
            LoadImages(size);
            
            _screenWidth = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.WorkArea.Height;
            
            Left = 50;
            Top = _screenHeight - Height - 50;
            
            System.Diagnostics.Debug.WriteLine($"TrayWindow position: Left={Left}, Top={Top}");
        }

        private void LoadImages(int size)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string grabPath = Path.Combine(exeDir, "FluentClip帧动画素材", "Grab", "Grab.png");
                string attachPath = Path.Combine(exeDir, "FluentClip帧动画素材", "Attach", "Attach_on.png");
                
                System.Diagnostics.Debug.WriteLine($"Loading Grab from: {grabPath}, exists: {File.Exists(grabPath)}");
                System.Diagnostics.Debug.WriteLine($"Loading Attach from: {attachPath}, exists: {File.Exists(attachPath)}");
                
                if (File.Exists(grabPath))
                {
                    _grabImage = new BitmapImage();
                    _grabImage.BeginInit();
                    _grabImage.UriSource = new Uri(grabPath, UriKind.Absolute);
                    _grabImage.CacheOption = BitmapCacheOption.OnLoad;
                    _grabImage.EndInit();
                }
                
                if (File.Exists(attachPath))
                {
                    _attachImage = new BitmapImage();
                    _attachImage.BeginInit();
                    _attachImage.UriSource = new Uri(attachPath, UriKind.Absolute);
                    _attachImage.CacheOption = BitmapCacheOption.OnLoad;
                    _attachImage.EndInit();
                    System.Diagnostics.Debug.WriteLine($"Attach image loaded successfully: {attachPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Attach image NOT found: {attachPath}");
                }
                
                if (_grabImage != null)
                {
                    TrayImage.Source = _grabImage;
                    TrayImage.Width = size;
                    TrayImage.Height = (int)(size * 1.333);
                    System.Diagnostics.Debug.WriteLine("Images loaded successfully");
                }
                else
                {
                    ShowPlaceholder();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}");
                ShowPlaceholder();
            }
        }

        private DispatcherTimer? _attachTimer;
        private const double AsymptoteX = -99;
        private const double Gravity = 0.15;
        
        private double _screenWidth;
        private double _screenHeight;
        private double _mainWindowTop;
        private double _mainWindowLeft;
        
        public void SetMainWindowTop(double top)
        {
            _mainWindowTop = top;
        }
        
        public void SetMainWindowLeft(double left)
        {
            _mainWindowLeft = left;
        }
        
        private void StartPhysics()
        {
            _screenWidth = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.WorkArea.Height;
            
            if (_physicsTimer != null) return;
            
            _isFalling = true;
            _isAttached = false;
            _velocityX = 0;
            _rotationAngle = 0;
            _rotationTime = 0;
            
            if (_mainWindow != null)
            {
                _mainWindowTop = _mainWindow.Top;
                _mainWindowLeft = _mainWindow.Left;
            }
            
            if (_mainWindowTop > 100 && _mainWindowTop < _screenHeight - 100)
            {
                Top = _mainWindowTop;
            }
            else
            {
                Top = _screenHeight / 3;
            }
            
            if (_mainWindowLeft > 0 && _mainWindowLeft < _screenWidth - 100)
            {
                Left = _mainWindowLeft;
            }
            else
            {
                Left = 50;
            }
            
            if (_grabImage != null)
            {
                Width = 450;
                Height = 450;
                TrayImage.Margin = new Thickness(100, -153, 0, 0);
                TrayImage.Source = _grabImage;
                TrayImage.Width = 220;
                TrayImage.Height = 294;
                ImageRotation.CenterX = 110;
                ImageRotation.CenterY = 147;
                ImageRotation.Angle = 0;
                BoundaryRect.Height = Height;
            }
            
            _physicsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _physicsTimer.Tick += PhysicsTick;
            _physicsTimer.Start();
            
            StartAttachTimer();
        }

        private void StartAttachTimer()
        {
            _attachTimer?.Stop();
            _attachTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _attachTimer.Tick += (s, e) =>
            {
                _attachTimer?.Stop();
                if (_isFalling && !_isDragging && IsNearAsymptote())
                {
                    SwitchToAttached();
                    _isAttached = true;
                    _isFalling = false;
                    StopPhysics();
                }
            };
            _attachTimer.Start();
        }

        private bool IsNearAsymptote()
        {
            double rightEdge = _screenWidth;
            return Left + Width >= rightEdge;
        }

        private void ResetAttachTimer()
        {
            if (_isAttached || !_isFalling) return;
            StartAttachTimer();
        }

        private void PhysicsTick(object? sender, EventArgs e)
        {
            if (!_isFalling || _isDragging)
            {
                StopPhysics();
                return;
            }
            
            Left += _velocityX;
            _velocityX += Gravity;
            
            _rotationTime += 1;
            double progress = Math.Min(_rotationTime / 100.0, 1.0);
            double easedProgress = 1 - Math.Pow(1 - progress, 3);
            _rotationAngle = easedProgress * 90;
            ImageRotation.Angle = _rotationAngle;
            
            double rightEdge = _screenWidth - AsymptoteX;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] screenWidth={_screenWidth}, rightEdge={rightEdge}, Left={Left}, Width={Width}, Left+Width={Left + Width}");
            if (Left + Width >= rightEdge)
            {
                Left = Math.Min(rightEdge - Width, _screenWidth - Width);
                _isFalling = false;
                _isAttached = true;
                StopPhysics();
                SwitchToAttached();
                return;
            }
            
            ResetAttachTimer();
        }

        private void StopPhysics()
        {
            if (_physicsTimer != null)
            {
                _physicsTimer.Stop();
                _physicsTimer = null;
            }
            _attachTimer?.Stop();
            _isFalling = false;
        }

        private void SwitchToAttached()
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SwitchToAttached called, _attachImage={_attachImage != null}");
            if (_attachImage != null)
            {
                TrayImage.Source = _attachImage;
                Width = 150;
                Height = 150;
                TrayImage.Margin = new Thickness(0, -40, 0, 0);
                TrayImage.Width = Width;
                TrayImage.Height = Height;
                ImageRotation.CenterX = Width / 2;
                ImageRotation.CenterY = Height / 2;
                BoundaryRect.Height = 400;
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.WorkArea.Height;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] SwitchToAttached: screenWidth={screenWidth}, AsymptoteX={AsymptoteX}, Width={Width}, Left={screenWidth - AsymptoteX - Width}");
                Left = screenWidth - Width + 108;
                Top = Top + 240;
                ImageRotation.Angle = -90;
            }
        }

        private void SwitchToGrab()
        {
            if (_grabImage != null && TrayImage != null)
            {
                Width = 450;
                Height = 450;
                TrayImage.Margin = new Thickness(100, -153, 0, 0);
                
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                if (Left + Width > screenWidth)
                {
                    Left = screenWidth - Width - 10;
                }
                if (Left < 0) Left = 10;
                
                TrayImage.Source = _grabImage;
                TrayImage.Width = 220;
                TrayImage.Height = 294;
                ImageRotation.CenterX = 110;
                ImageRotation.CenterY = 147;
                ImageRotation.Angle = 0;
                BoundaryRect.Height = Height;
            }
        }

        public void ShowWindow()
    {
        try
        {
            _screenWidth = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.WorkArea.Height;
            
            if (_mainWindow != null)
            {
                _mainWindowTop = _mainWindow.Top;
                _mainWindowLeft = _mainWindow.Left;
                
                if (_mainWindowTop > 100 && _mainWindowTop < _screenHeight - 100)
                {
                    Top = _mainWindowTop;
                }
                else
                {
                    Top = _screenHeight / 3;
                }
                
                if (_mainWindowLeft > 0 && _mainWindowLeft < _screenWidth - 100)
                {
                    Left = _mainWindowLeft;
                }
                else
                {
                    Left = 50;
                }
            }
            else
            {
                Top = _screenHeight / 3;
                Left = 50;
            }
            
            _isAttached = false;
            
            Show();
            Activate();
            Topmost = true;
            Focus();
            
            StartPhysics();
            StartHeartbeat();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"显示悬浮窗失败: {ex.Message}");
        }
    }

    private void ShowPlaceholder()
    {
        System.Diagnostics.Debug.WriteLine("图片加载失败，悬浮窗显示占位符");
        MainGrid.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(100, 150, 200));
        MainGrid.Opacity = 1.0;
    }

        public static TrayWindow? Instance => _instance;

        private int GetTrayPositionX(int scaledWidth)
        {
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            
            int x = screenWidth - scaledWidth - 20;
            System.Diagnostics.Debug.WriteLine($"X calculation: screenWidth={screenWidth}, scaledWidth={scaledWidth}, result={x}");
            return Math.Max(0, x);
        }

        private int GetTrayPositionY(int scaledHeight)
        {
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            int workAreaHeight = (int)SystemParameters.WorkArea.Height;
            
            int y = workAreaHeight - scaledHeight - 20;
            System.Diagnostics.Debug.WriteLine($"Y calculation: workAreaHeight={workAreaHeight}, scaledHeight={scaledHeight}, result={y}");
            return Math.Max(0, y);
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            _attachTimer?.Stop();
            
            StopPhysics();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point currentPos = e.GetPosition(this);
                double offsetX = Math.Abs(currentPos.X - _dragStartPoint.X);
                double offsetY = Math.Abs(currentPos.Y - _dragStartPoint.Y);
                
                if (offsetX > 3 || offsetY > 3)
                {
                    _isDragging = true;
                    CaptureMouse();
                    _attachTimer?.Stop();
                }
            }
            
            if (_isDragging)
            {
                ResetAttachTimer();
                Point currentPos = e.GetPosition(this);
                double offsetX = currentPos.X - _dragStartPoint.X;
                double offsetY = currentPos.Y - _dragStartPoint.Y;
                
                Left = Left + offsetX;
                Top = Top + offsetY;
                _dragStartPoint = currentPos;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                
                if (_isAttached)
                {
                    if (!IsNearAsymptote())
                    {
                        _isAttached = false;
                        _isFalling = false;
                        SwitchToGrab();
                    }
                    else
                    {
                        UpdateMainWindowPosition();
                        ShowMainWindow();
                    }
                }
                else if (_isFalling)
                {
                    StartPhysics();
                }
                else
                {
                    if (IsNearAsymptote())
                    {
                        _isAttached = true;
                        SwitchToAttached();
                        Top = Top - 160;
                    }
                }
            }
            else
            {
                UpdateMainWindowPosition();
                ShowMainWindow();
            }
        }
        
        private void UpdateMainWindowPosition()
        {
            if (_mainWindow != null)
            {
                _mainWindowTop = _mainWindow.Top;
                _mainWindowLeft = _mainWindow.Left;
            }
        }

        public void ShowMainWindow()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.WorkArea.Height;
            
            if (_mainWindow != null)
            {
                double newLeft;
                double newTop;
                
                if (_isAttached)
                {
                    newLeft = Left - _mainWindow.Width - 20;
                    newTop = Top;
                }
                else
                {
                    newLeft = Left;
                    newTop = Top;
                }
                
                if (newLeft < 0) newLeft = 10;
                if (newLeft + _mainWindow.Width > screenWidth)
                {
                    newLeft = screenWidth - _mainWindow.Width - 10;
                }
                if (newTop + _mainWindow.Height > screenHeight)
                {
                    newTop = screenHeight - _mainWindow.Height - 10;
                }
                
                _mainWindow.Left = newLeft;
                _mainWindow.Top = newTop;
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
                _mainWindow.Focus();
            }
            else
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    double newLeft;
                    double newTop;
                    
                    if (_isAttached)
                    {
                        newLeft = Left - mainWindow.Width - 20;
                        newTop = Top;
                    }
                    else
                    {
                        newLeft = Left;
                        newTop = Top;
                    }
                    
                    if (newLeft < 0) newLeft = 10;
                    if (newLeft + mainWindow.Width > screenWidth)
                    {
                        newLeft = screenWidth - mainWindow.Width - 10;
                    }
                    if (newTop + mainWindow.Height > screenHeight)
                    {
                        newTop = screenHeight - mainWindow.Height - 10;
                    }
                    
                    mainWindow.Left = newLeft;
                    mainWindow.Top = newTop;
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
            }
            
            StopHeartbeat();
            Hide();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            var storyboard = FindResource("HoverAnimation") as Storyboard;
            storyboard?.Begin(this);
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            var storyboard = FindResource("LeaveAnimation") as Storyboard;
            storyboard?.Begin(this);
        }

        public void StartClipboardMonitoring()
        {
            if (_clipboardService != null) return;
            
            _clipboardService = new ClipboardService();
            _clipboardService.ClipboardChanged += OnClipboardChanged;
            _clipboardService.StartMonitoring(this);
        }

        public void StopClipboardMonitoring()
        {
            _clipboardService?.StopMonitoring();
            _clipboardService?.Dispose();
            _clipboardService = null;
        }

        private void OnClipboardChanged(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] OnClipboardChanged called");
                
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count > 0)
                    {
                        string? filePath = files[0];
                        if (!string.IsNullOrEmpty(filePath) && filePath != _lastClipboardContent)
                        {
                            _lastClipboardContent = filePath;
                            
                            string extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
                            string fileType = GetFileTypeName(extension);
                            
                            Dispatcher.Invoke(() =>
                            {
                                ShowBubbleNotification(filePath, fileType);
                            });
                        }
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    string currentContent = "image_" + Clipboard.GetImage()?.GetHashCode();
                    if (currentContent != _lastClipboardContent)
                    {
                        _lastClipboardContent = currentContent;
                        
                        Dispatcher.Invoke(() =>
                        {
                            ShowBubbleNotification(null, "image");
                        });
                    }
                }
                else if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text) && text != _lastClipboardContent)
                    {
                        _lastClipboardContent = text;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard changed error: {ex.Message}");
            }
        }

        private string GetFileTypeName(string extension)
        {
            return extension switch
            {
                "png" or "jpg" or "jpeg" or "gif" or "bmp" or "webp" or "ico" => "image",
                "pdf" => "pdf",
                "doc" or "docx" => "word",
                "xls" or "xlsx" => "excel",
                "ppt" or "pptx" => "ppt",
                "zip" or "rar" or "7z" => "压缩包",
                "txt" => "文本",
                "mp3" or "wav" or "flac" or "aac" => "音频",
                "mp4" or "avi" or "mkv" or "mov" => "视频",
                "exe" or "msi" => "程序",
                _ => extension
            };
        }

        private BubbleWindow? _bubbleWindow;
        private DispatcherTimer? _heartbeatTimer;
        private readonly Random _heartbeatRandom = new();
        private readonly List<string> _heartbeatTopics = new()
        {
            "最新科技新闻",
            "今日热点事件",
            "AI人工智能新闻",
            "数码科技热门",
            "游戏新闻",
            "有趣冷知识",
            "天气怎么样",
            "最近流行的梗",
            "科技圈发生了什么",
            "有什么好玩的消息"
        };

        private void ShowBubbleNotification(string? filePath = null, string? fileType = null)
        {
            string message = BubbleMessages.GetSmartMessage(filePath, fileType);
            
            _bubbleWindow?.Close();
            
            _bubbleWindow = new BubbleWindow();
            
            double bubbleLeft = _isAttached ? Left - 250 + 30 : Left + 30;
            _bubbleWindow.ShowAt(bubbleLeft, Top + 80, message);
        }

        private void StartHeartbeat()
        {
            if (_heartbeatTimer != null) return;

            _heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _heartbeatTimer.Tick += async (s, e) => await OnHeartbeatTick();
            _heartbeatTimer.Start();
            System.Diagnostics.Debug.WriteLine("[DEBUG] Heartbeat timer started");
        }

        private void StopHeartbeat()
        {
            _heartbeatTimer?.Stop();
            _heartbeatTimer = null;
            System.Diagnostics.Debug.WriteLine("[DEBUG] Heartbeat timer stopped");
        }

        private async Task OnHeartbeatTick()
        {
            if (!_isFalling && !_isAttached) return;
            if (_heartbeatRandom.Next(100) >= 25) return;

            try
            {
                string topic = _heartbeatTopics[_heartbeatRandom.Next(_heartbeatTopics.Count)];
                string searchResult = await SearchWebForHeartbeat(topic);
                
                if (!string.IsNullOrEmpty(searchResult))
                {
                    string message = GenerateHeartbeatMessage(topic, searchResult);
                    Dispatcher.Invoke(() =>
                    {
                        ShowHeartbeatBubble(message);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Heartbeat error: {ex.Message}");
            }
        }

        private async Task<string> SearchWebForHeartbeat(string query)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                string searchUrl = $"https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}&rn=5";
                var response = await httpClient.GetStringAsync(searchUrl);

                var titles = new List<string>();
                var titleMatches = Regex.Matches(response, @"<h3[^>]*class=""?[^\""^>]*t""?[^>]*>([^<]+)</h3>", RegexOptions.IgnoreCase);
                foreach (Match match in titleMatches)
                {
                    if (titleMatches.Count > 0)
                    {
                        string title = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "");
                        title = System.Net.WebUtility.HtmlDecode(title).Trim();
                        if (title.Length > 5 && title.Length < 80)
                        {
                            titles.Add(title);
                        }
                    }
                }

                if (titles.Count > 0)
                {
                    var selectedTitles = titles.Take(3).ToList();
                    return string.Join(" | ", selectedTitles);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GenerateHeartbeatMessage(string topic, string searchResult)
        {
            var templates = new List<string>
            {
                "喵~ {0}了解一下~ {1}",
                "哦~ {0} {1} 雫觉得很有趣呢喵~",
                "嗯~ 雫看到关于{0}的消息了~ {1}",
                "喵~ {0} {1} 主人要不要看看呀~",
                "哎呀~ {0} {1} 雫分享给你喵~"
            };

            string template = templates[_heartbeatRandom.Next(templates.Count)];
            return string.Format(template, topic, searchResult);
        }

        private void ShowHeartbeatBubble(string message)
        {
            _bubbleWindow?.Close();
            
            _bubbleWindow = new BubbleWindow();
            
            double bubbleLeft = _isAttached ? Left - 250 + 30 : Left + 30;
            _bubbleWindow.ShowAt(bubbleLeft, Top + 80, message);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
