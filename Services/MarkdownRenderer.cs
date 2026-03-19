using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FluentClip.Services;

public static class MarkdownRenderer
{
    private static readonly Dictionary<string, SolidColorBrush> CodeLanguageColors = new()
    {
        { "python", new SolidColorBrush(Color.FromRgb(53, 114, 165)) },
        { "javascript", new SolidColorBrush(Color.FromRgb(247, 223, 30)) },
        { "js", new SolidColorBrush(Color.FromRgb(247, 223, 30)) },
        { "typescript", new SolidColorBrush(Color.FromRgb(0, 122, 204)) },
        { "ts", new SolidColorBrush(Color.FromRgb(0, 122, 204)) },
        { "c#", new SolidColorBrush(Color.FromRgb(104, 33, 122)) },
        { "csharp", new SolidColorBrush(Color.FromRgb(104, 33, 122)) },
        { "java", new SolidColorBrush(Color.FromRgb(176, 114, 25)) },
        { "go", new SolidColorBrush(Color.FromRgb(0, 173, 216)) },
        { "rust", new SolidColorBrush(Color.FromRgb(222, 165, 132)) },
        { "ruby", new SolidColorBrush(Color.FromRgb(204, 52, 45)) },
        { "php", new SolidColorBrush(Color.FromRgb(119, 123, 180)) },
        { "swift", new SolidColorBrush(Color.FromRgb(240, 81, 56)) },
        { "kotlin", new SolidColorBrush(Color.FromRgb(169, 123, 255)) },
        { "sql", new SolidColorBrush(Color.FromRgb(0, 82, 136)) }
    };

    private static readonly SolidColorBrush DefaultCodeColor = new(Color.FromRgb(244, 244, 244));

    public static FrameworkElement Render(string markdown)
    {
        var panel = new StackPanel();
        var blocks = ParseBlocks(markdown);

        foreach (var block in blocks)
        {
            var element = RenderBlock(block);
            if (element != null)
            {
                panel.Children.Add(element);
            }
        }

        return panel;
    }

    public static FrameworkElement RenderWithThinking(string markdown)
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "think_debug.log");
        
        try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)); } catch { }
        
        var panel = new StackPanel();

        var thinkingContent = ExtractThinking(markdown);
        var cleanMarkdown = RemoveThinking(markdown);

        File.AppendAllText(logPath, $"[RenderWithThinking] Input length: {markdown.Length}, Think extracted: {thinkingContent.Length}\n");
        
        if (!string.IsNullOrEmpty(thinkingContent))
        {
            File.AppendAllText(logPath, $"[RenderWithThinking] Adding thinking block: {thinkingContent.Substring(0, Math.Min(30, thinkingContent.Length))}...\n");
            panel.Children.Add(RenderThinking(thinkingContent));
        }

        var blocks = ParseBlocks(cleanMarkdown);

        foreach (var block in blocks)
        {
            var element = RenderBlock(block);
            if (element != null)
            {
                panel.Children.Add(element);
            }
        }

        return panel;
    }

    private static string ExtractThinking(string markdown)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "think_debug.log");
            
            var match = Regex.Match(markdown, @"<think>[\s\S]*?</think>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var content = match.Value;
                content = content.Replace("<think>", "").Replace("</think>", "").Trim();
                File.AppendAllText(logPath, $"[Extract] Found think: {content.Substring(0, Math.Min(50, content.Length))}...\n");
                return content;
            }

            match = Regex.Match(markdown, @"<thinking>[\s\S]*?</thinking>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var content = match.Value;
                content = content.Replace("<thinking>", "").Replace("</thinking>", "").Trim();
                File.AppendAllText(logPath, $"[Extract] Found thinking: {content.Substring(0, Math.Min(50, content.Length))}...\n");
                return content;
            }
            
            if (markdown.Contains("<think>") || markdown.Contains("</thinking>"))
            {
                File.AppendAllText(logPath, $"[Extract] Contains think marker but regex failed!\nInput: {markdown.Substring(0, Math.Min(100, markdown.Length))}\n");
            }
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "think_debug.log");
            File.AppendAllText(logPath, $"[Extract] Error: {ex.Message}\n");
        }
        
        return "";
    }

    private static string RemoveThinking(string markdown)
    {
        try
        {
            markdown = Regex.Replace(markdown, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<thinking>[\s\S]*?</thinking>", "", RegexOptions.IgnoreCase);
        }
        catch { }

        return markdown.Trim();
    }

    private static FrameworkElement RenderThinking(string thinkingContent)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 245)),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 220)),
            BorderThickness = new Thickness(1)
        };

        var mainStack = new StackPanel();

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = Cursors.Hand,
            Tag = false
        };

        var toggleIcon = new TextBlock
        {
            Text = "▶",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var headerText = new TextBlock
        {
            Text = "💭 AI正在思考...",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            FontStyle = FontStyles.Italic
        };

        headerPanel.Children.Add(toggleIcon);
        headerPanel.Children.Add(headerText);

        var contentPanel = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var thinkingText = new TextBlock
        {
            Text = thinkingContent,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            TextWrapping = TextWrapping.Wrap,
            FontStyle = FontStyles.Italic
        };
        contentPanel.Children.Add(thinkingText);

        headerPanel.MouseLeftButtonDown += (s, e) =>
        {
            var isExpanded = (bool)headerPanel.Tag;
            if (isExpanded)
            {
                contentPanel.Visibility = Visibility.Collapsed;
                toggleIcon.Text = "▶";
                headerText.Text = "💭 AI正在思考...";
                headerPanel.Tag = false;
            }
            else
            {
                contentPanel.Visibility = Visibility.Visible;
                toggleIcon.Text = "▼";
                headerText.Text = "💭 思考中...";
                headerPanel.Tag = true;
            }
        };

        mainStack.Children.Add(headerPanel);
        mainStack.Children.Add(contentPanel);
        border.Child = mainStack;

        return border;
    }

    private class Block
    {
        public string Type { get; set; } = "";
        public string Content { get; set; } = "";
        public string Language { get; set; } = "";
    }

    private static List<Block> ParseBlocks(string markdown)
    {
        var blocks = new List<Block>();
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var codeContent = "";
        var codeLanguage = "";
        var currentText = "";

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    if (!string.IsNullOrEmpty(currentText.Trim()))
                    {
                        blocks.Add(new Block { Type = "text", Content = currentText.Trim() });
                        currentText = "";
                    }
                    inCodeBlock = true;
                    codeLanguage = line.Trim().Substring(3).Trim();
                    codeContent = "";
                }
                else
                {
                    blocks.Add(new Block { Type = "code", Content = codeContent.Trim(), Language = codeLanguage });
                    inCodeBlock = false;
                    codeContent = "";
                    codeLanguage = "";
                }
            }
            else if (inCodeBlock)
            {
                codeContent += line + "\n";
            }
            else
            {
                currentText += line + "\n";
            }
        }

        if (!string.IsNullOrEmpty(currentText.Trim()))
        {
            blocks.Add(new Block { Type = "text", Content = currentText.Trim() });
        }

        return blocks;
    }

    private static FrameworkElement? RenderBlock(Block block)
    {
        if (block.Type == "code")
        {
            return RenderCodeBlock(block.Content, block.Language);
        }

        return RenderTextBlock(block.Content);
    }

    private static FrameworkElement RenderTextBlock(string text)
    {
        var panel = new StackPanel();

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("# "))
            {
                panel.Children.Add(CreateTextBlock(trimmedLine.Substring(2), 18, FontWeights.Bold, "#1C1C1E"));
                continue;
            }
            if (trimmedLine.StartsWith("## "))
            {
                panel.Children.Add(CreateTextBlock(trimmedLine.Substring(3), 16, FontWeights.SemiBold, "#1C1C1E"));
                continue;
            }
            if (trimmedLine.StartsWith("### "))
            {
                panel.Children.Add(CreateTextBlock(trimmedLine.Substring(4), 14, FontWeights.SemiBold, "#1C1C1E"));
                continue;
            }
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
            {
                var bulletText = trimmedLine.Substring(2);
                var bulletPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                bulletPanel.Children.Add(CreateTextBlock("• ", 12, FontWeights.Normal, "#6E6E73"));
                bulletPanel.Children.Add(CreateInlineTextBlock(bulletText, 12, "#3D3D3D"));
                panel.Children.Add(bulletPanel);
                continue;
            }
            if (Regex.IsMatch(trimmedLine, @"^\d+\.\s"))
            {
                var match = Regex.Match(trimmedLine, @"^(\d+)\.\s(.*)");
                if (match.Success)
                {
                    var numPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    numPanel.Children.Add(CreateTextBlock(match.Groups[1].Value + ". ", 12, FontWeights.Normal, "#6E6E73"));
                    numPanel.Children.Add(CreateInlineTextBlock(match.Groups[2].Value, 12, "#3D3D3D"));
                    panel.Children.Add(numPanel);
                    continue;
                }
            }

            panel.Children.Add(CreateInlineTextBlock(line, 12, "#3D3D3D"));
        }

        return panel;
    }

    private static TextBlock CreateTextBlock(string text, double fontSize, FontWeight fontWeight, string colorHex)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private static TextBlock CreateInlineTextBlock(string text, double fontSize, string colorHex)
    {
        var textBlock = new TextBlock
        {
            FontSize = fontSize,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        };

        text = text.Replace("\\*\\*", "||BOLD||");
        text = text.Replace("\\*", "||ITALIC||");
        text = text.Replace("`", "||CODE||");

        var segments = Regex.Split(text, @"(\*\*|[\*`]|\[\d+\])");

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment)) continue;

            if (segment == "**")
            {
                continue;
            }
            else if (segment.StartsWith("**") && segment.EndsWith("**"))
            {
                var boldText = segment.TrimStart('*').TrimEnd('*');
                textBlock.Inlines.Add(new Run(boldText) { FontWeight = FontWeights.Bold });
            }
            else if (segment == "`")
            {
                continue;
            }
            else if (segment.StartsWith("`") && segment.EndsWith("`"))
            {
                var codeText = segment.Trim('`');
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(244, 244, 244)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(2, 0, 2, 0)
                };
                border.Child = new TextBlock
                {
                    Text = codeText,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = fontSize - 1,
                    Foreground = new SolidColorBrush(Color.FromRgb(227, 76, 38))
                };
                textBlock.Inlines.Add(new InlineUIContainer(border));
            }
            else
            {
                var processedSegment = segment
                    .Replace("||BOLD||", "**")
                    .Replace("||ITALIC||", "*")
                    .Replace("||CODE||", "`");

                textBlock.Inlines.Add(new Run(processedSegment));
            }
        }

        return textBlock;
    }

    private static Border RenderCodeBlock(string code, string language)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 8, 0, 8),
            Padding = new Thickness(12)
        };

        var panel = new StackPanel();

        if (!string.IsNullOrEmpty(language))
        {
            var langLabel = new TextBlock
            {
                Text = language.ToLower(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 156, 156)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(langLabel);
        }

        var codeBlock = new TextBlock
        {
            Text = code,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(233, 233, 233)),
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(codeBlock);

        border.Child = panel;
        return border;
    }
}
