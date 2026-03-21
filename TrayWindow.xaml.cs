using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        private double _rotationVelocity = 0;
        private bool _isFalling = false;
        private bool _isAttached = false;
        private BitmapImage? _grabImage;
        private BitmapImage? _attachImage;
        private DispatcherTimer? _physicsTimer;

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
        private const double AsymptoteX = 80;
        private const double Gravity = 0.5;
        private const double RotationGravity = 2.0;
        
        private double _screenWidth;
        private double _screenHeight;
        
        private void StartPhysics()
        {
            _screenWidth = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.WorkArea.Height;
            
            if (_physicsTimer != null) return;
            
            _isFalling = true;
            _isAttached = false;
            _velocityX = 0;
            _rotationAngle = 0;
            _rotationVelocity = 0;
            
            Top = _screenHeight - Height - 50;
            
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
            double rightEdge = _screenWidth - AsymptoteX;
            return Left + Width >= rightEdge - 50;
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
            
            _rotationVelocity += RotationGravity;
            _rotationAngle += _rotationVelocity;
            ImageRotation.Angle = _rotationAngle;
            
            double rightEdge = _screenWidth - AsymptoteX;
            if (Left + Width >= rightEdge)
            {
                Left = rightEdge - Width;
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
            if (_attachImage != null)
            {
                TrayImage.Source = _attachImage;
                Width = 60;
                Height = 60;
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                Left = screenWidth - AsymptoteX - Width / 2;
                Top = Math.Max(100, Top);
                ImageRotation.Angle = 0;
            }
        }

        private void SwitchToGrab()
        {
            if (_grabImage != null && TrayImage != null)
            {
                int size = 200;
                TrayImage.Source = _grabImage;
                Width = size;
                Height = (int)(size * 1.333);
            }
        }

        public void ShowWindow()
    {
        try
        {
            Show();
            Activate();
            Topmost = true;
            Focus();
            
            if (!_isAttached)
            {
                StartPhysics();
            }
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
            
            if (_isAttached)
            {
                SwitchToGrab();
                _isAttached = false;
                _isFalling = true;
                _velocityX = -8;
                _rotationVelocity = 0;
            }
            
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
                
                if (!_isAttached && _isFalling)
                {
                    StartPhysics();
                }
            }
            else
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
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
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
            }
            
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
