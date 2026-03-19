using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace FluentClip.Models;

public enum ClipboardItemType
{
    Text,
    Image,
    File
}

public enum FileCategory
{
    Unknown,
    Image,
    Video,
    Audio,
    Document,
    Other
}

public class ClipboardItem
{
    public ClipboardItemType ItemType { get; set; }
    public string? TextContent { get; set; }
    public BitmapSource? ImageContent { get; set; }
    public string[]? FilePaths { get; set; }
    public DateTime Timestamp { get; set; }

    public BitmapSource? Thumbnail { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public BitmapSource? AlbumArt { get; set; }

    public FileCategory FileCategory => GetFileCategory();

    private FileCategory GetFileCategory()
    {
        if (ItemType != ClipboardItemType.File || FilePaths == null || FilePaths.Length == 0)
            return FileCategory.Unknown;

        var ext = Path.GetExtension(FilePaths[0])?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" => FileCategory.Image,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" => FileCategory.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" => FileCategory.Audio,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => FileCategory.Document,
            _ => FileCategory.Other
        };
    }

    public string DisplayText
    {
        get
        {
            if (ItemType == ClipboardItemType.Text)
                return TextContent?.Length > 50 ? TextContent[..50] + "..." : TextContent ?? "";
            
            if (ItemType == ClipboardItemType.Image && FilePaths?.Length > 0)
            {
                var fileName = Path.GetFileName(FilePaths[0]);
                return fileName.Length > 30 ? fileName[..30] + "..." : fileName;
            }
            
            if (ItemType == ClipboardItemType.Image)
                return "图片";
            
            if (ItemType == ClipboardItemType.File && FilePaths?.Length > 0)
            {
                var fileName = Path.GetFileName(FilePaths[0]);
                
                if (FileCategory == FileCategory.Audio && !string.IsNullOrEmpty(Artist))
                {
                    return $"{Artist} - {fileName}";
                }
                
                return fileName.Length > 30 ? fileName[..30] + "..." : fileName;
            }
            
            return "文件";
        }
    }

    public string FullPath => ItemType switch
    {
        ClipboardItemType.File => FilePaths?.FirstOrDefault() ?? "",
        _ => ""
    };
}
