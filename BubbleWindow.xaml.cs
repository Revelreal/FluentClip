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
        private const double CatEmojiHeight = 28;   // 猫 emoji 高度 + margin
        private const double TextHeight = 20;       // 每行文字高度
        private const double PaddingHeight = 50;    // Border padding + StackPanel margin
        private const double MinBubbleHeight = 80;  // 最小高度

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
            double maxWidth = MessageText.MaxWidth > 0 ? MessageText.MaxWidth : 250;

            var formattedText = new FormattedText(
                message,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(MessageText.FontFamily, MessageText.FontStyle, MessageText.FontWeight, MessageText.FontStretch),
                MessageText.FontSize,
                MessageText.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = maxWidth
            };

            double textWidth = formattedText.WidthIncludingTrailingWhitespace;
            int lineCount = (int)Math.Ceiling(textWidth / maxWidth);
            if (lineCount < 1) lineCount = 1;

            // 计算总高度：猫 emoji + 文字行数 + padding
            double requiredHeight = CatEmojiHeight + (lineCount * TextHeight) + PaddingHeight;

            if (requiredHeight < MinBubbleHeight)
            {
                requiredHeight = MinBubbleHeight;
            }

            Height = requiredHeight;
        }
    }
}
