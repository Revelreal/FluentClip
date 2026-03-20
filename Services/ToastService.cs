using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;

namespace FluentClip.Services;

public class ToastService
{
    private readonly Window _window;
    private Border? _currentToast;
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    public ToastService(Window window)
    {
        _window = window;
    }

    public void ShowCopySuccess()
    {
        ShowToast("已复制到剪贴板");
    }

    public void ShowToast(string message, int durationMs = 2000)
    {
        _window.Dispatcher.Invoke(() =>
        {
            if (_currentToast != null)
            {
                _window.Resources.Remove(_currentToast.Name);
                _window.Content?.GetType().GetField("_currentToast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_window.Content, null);
            }

            var toast = new Border
            {
                Name = "ToastBorder",
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 50, 50, 50)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 80),
                Opacity = 0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.3
                }
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13
            };
            toast.Child = textBlock;

            var mainGrid = FindMainGrid();
            if (mainGrid != null)
            {
                Grid.SetRow(toast, 2);
                mainGrid.Children.Add(toast);
            }

            _currentToast = toast;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                BeginTime = TimeSpan.FromMilliseconds(durationMs)
            };

            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _toastTimer?.Stop();
            _toastTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs + 200)
            };
            _toastTimer.Tick += (s, e) =>
            {
                toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                fadeOut.Completed += (s2, e2) =>
                {
                    if (mainGrid != null && toast.Parent == mainGrid)
                    {
                        mainGrid.Children.Remove(toast);
                    }
                    _currentToast = null;
                };
                _toastTimer.Stop();
            };
            _toastTimer.Start();
        });
    }

    private Grid? FindMainGrid()
    {
        if (_window.Content is Grid grid)
            return grid;

        if (_window.Content is Border border && border.Child is Grid borderGrid)
            return borderGrid;

        return null;
    }
}
