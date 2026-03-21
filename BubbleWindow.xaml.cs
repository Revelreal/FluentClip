using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FluentClip
{
    public partial class BubbleWindow : Window
    {
        private DispatcherTimer? _hideTimer;
        private const double BaseHeight = 60;
        private const double TextHeight = 22;
        private const double PaddingHeight = 40;
        private const double MaxBubbleHeight = 400;

        public BubbleWindow()
        {
            InitializeComponent();
        }

        public void ShowAt(double left, double top, string message)
        {
            Left = left;
            Top = top;
            MessageText.Text = message;

            MeasureTextHeight(message);

            Show();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
            BeginAnimation(OpacityProperty, fadeIn);

            _hideTimer?.Stop();
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
                fadeOut.Completed += (s2, e2) =>
                {
                    Close();
                };
                BeginAnimation(OpacityProperty, fadeOut);
            };
            _hideTimer.Start();
        }

        private void MeasureTextHeight(string message)
        {
            var formattedText = new FormattedText(
                message,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(MessageText.FontFamily, MessageText.FontStyle, MessageText.FontWeight, MessageText.FontStretch),
                MessageText.FontSize,
                MessageText.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textWidth = formattedText.WidthIncludingTrailingWhitespace;
            int lineCount = (int)Math.Ceiling(textWidth / 250);
            if (lineCount < 1) lineCount = 1;

            double requiredHeight = PaddingHeight + (lineCount * TextHeight) + 10;
            if (requiredHeight > MaxBubbleHeight)
            {
                requiredHeight = MaxBubbleHeight;
            }
            else if (requiredHeight < BaseHeight)
            {
                requiredHeight = BaseHeight;
            }

            Height = requiredHeight;
        }
    }
}
