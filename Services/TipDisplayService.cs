using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FluentClip.Services;

public class TipDisplayService
{
    private readonly Random _random = new();
    private readonly HashSet<int> _shownTipIndices = new();

    private static readonly List<string> Tips = new()
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

    public void ShowRandomTip(Panel container, ScrollViewer scrollViewer)
    {
        if (_shownTipIndices.Count >= Tips.Count)
        {
            _shownTipIndices.Clear();
        }

        if (_random.Next(100) < 40)
        {
            var availableIndices = Enumerable.Range(0, Tips.Count).Where(i => !_shownTipIndices.Contains(i)).ToList();
            if (availableIndices.Count == 0)
            {
                _shownTipIndices.Clear();
                availableIndices = Enumerable.Range(0, Tips.Count).ToList();
            }

            int tipIndex = availableIndices[_random.Next(availableIndices.Count)];
            _shownTipIndices.Add(tipIndex);
            string tip = Tips[tipIndex];

            var tipBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 252)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = tip,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Opacity = 0.7
            };

            tipBorder.Child = tipText;
            container.Children.Add(tipBorder);
            scrollViewer.ScrollToEnd();
        }
    }

    public IReadOnlyList<string> GetAllTips() => Tips;
}
