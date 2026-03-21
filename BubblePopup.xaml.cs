using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace FluentClip
{
    public partial class BubblePopup : UserControl
    {
        private System.Windows.Threading.DispatcherTimer? _hideTimer;
        private Storyboard? _fadeInStoryboard;
        private Storyboard? _fadeOutStoryboard;

        public BubblePopup()
        {
            InitializeComponent();
            
            _fadeInStoryboard = FindResource("FadeIn") as Storyboard;
            _fadeOutStoryboard = FindResource("FadeOut") as Storyboard;
            
            System.Diagnostics.Debug.WriteLine($"[BubblePopup] Constructor: _fadeInStoryboard={_fadeInStoryboard != null}, _fadeOutStoryboard={_fadeOutStoryboard != null}");
        }

        public void Show(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[BubblePopup] Show called with message: {message}");
            
            MessageText.Text = message;
            
            System.Diagnostics.Debug.WriteLine($"[BubblePopup] Before show - Visibility: {Visibility}, Opacity: {Opacity}");
            
            this.Visibility = Visibility.Visible;
            this.Opacity = 1;
            
            System.Diagnostics.Debug.WriteLine($"[BubblePopup] After show - Visibility: {Visibility}, Opacity: {Opacity}");
            
            _fadeInStoryboard?.Begin(this);
            
            _hideTimer?.Stop();
            _hideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            _hideTimer.Tick += (s, e) =>
            {
                Hide();
            };
            _hideTimer.Start();
            
            System.Diagnostics.Debug.WriteLine($"[BubblePopup] Show completed");
        }

        public void Hide()
        {
            _hideTimer?.Stop();
            
            _fadeOutStoryboard?.Begin(this);
        }

        private void FadeOut_Completed(object? sender, EventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }
    }
}
