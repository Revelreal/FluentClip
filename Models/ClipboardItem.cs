using System;
using System.IO;
using System.Linq;

namespace FluentClip.Models;

public enum ClipboardItemType
{
    Text,
    Image,
    File
}

public class ClipboardItem
{
    public ClipboardItemType ItemType { get; set; }
    public string? TextContent { get; set; }
    public System.Windows.Media.Imaging.BitmapSource? ImageContent { get; set; }
    public string[]? FilePaths { get; set; }
    public DateTime Timestamp { get; set; }

    public string DisplayText => ItemType switch
    {
        ClipboardItemType.Text => TextContent?.Length > 50 ? TextContent[..50] + "..." : TextContent ?? "",
        ClipboardItemType.Image => "图片",
        ClipboardItemType.File => FilePaths?.FirstOrDefault() != null ? Path.GetFileName(FilePaths.First()) : "文件",
        _ => ""
    };

    public string FullPath => ItemType switch
    {
        ClipboardItemType.File => FilePaths?.FirstOrDefault() ?? "",
        _ => ""
    };
}
