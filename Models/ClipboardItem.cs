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
    public string Id { get; set; } = Guid.NewGuid().ToString();
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

    public string FileTypeFormat
    {
        get
        {
            if (ItemType == ClipboardItemType.Image && FilePaths?.Length > 0)
            {
                var ext = Path.GetExtension(FilePaths[0]).ToLowerInvariant();
                return ext switch
                {
                    ".jpg" or ".jpeg" => "图片 | JPG",
                    ".png" => "图片 | PNG",
                    ".gif" => "图片 | GIF",
                    ".bmp" => "图片 | BMP",
                    ".webp" => "图片 | WEBP",
                    ".svg" => "图片 | SVG",
                    ".tiff" or ".tif" => "图片 | TIFF",
                    ".ico" => "图片 | ICO",
                    _ => ext.Length > 0 ? $"图片 | {ext[1..].ToUpper()}" : "图片"
                };
            }
            
            if (ItemType == ClipboardItemType.Image)
            {
                return "图片";
            }
            
            if (ItemType == ClipboardItemType.File && FilePaths?.Length > 0)
            {
                if (Directory.Exists(FilePaths[0]))
                {
                    return "文件 | 文件夹";
                }
                
                var ext = Path.GetExtension(FilePaths[0]).ToLowerInvariant();
                var format = ext switch
                {
                    ".jpg" or ".jpeg" => "图片 | JPG",
                    ".png" => "图片 | PNG",
                    ".gif" => "图片 | GIF",
                    ".bmp" => "图片 | BMP",
                    ".webp" => "图片 | WEBP",
                    ".svg" => "图片 | SVG",
                    ".mp3" => "音乐 | MP3",
                    ".wav" => "音乐 | WAV",
                    ".flac" => "音乐 | FLAC",
                    ".aac" => "音乐 | AAC",
                    ".ogg" => "音乐 | OGG",
                    ".mp4" => "视频 | MP4",
                    ".avi" => "视频 | AVI",
                    ".mkv" => "视频 | MKV",
                    ".mov" => "视频 | MOV",
                    ".pdf" => "文档 | PDF",
                    ".doc" or ".docx" => "文档 | Word",
                    ".xls" or ".xlsx" => "文档 | Excel",
                    ".ppt" or ".pptx" => "文档 | PPT",
                    ".txt" => "文档 | TXT",
                    ".zip" => "压缩包 | ZIP",
                    ".rar" => "压缩包 | RAR",
                    ".7z" => "压缩包 | 7Z",
                    ".exe" => "程序 | EXE",
                    ".dll" => "程序 | DLL",
                    _ => ext.Length > 0 ? $"{ext[1..].ToUpper()} 文件" : "文件"
                };
                
                return format;
            }
            
            return "";
        }
    }

    public string TextTypeFormat
    {
        get
        {
            if (ItemType != ClipboardItemType.Text || string.IsNullOrEmpty(TextContent))
                return "";
            
            var text = TextContent.Trim();
            
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && 
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return "文本 | 链接";
            }
            
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "文本 | 链接";
            }
            
            if (text.StartsWith("#"))
            {
                return "文本 | Markdown标题";
            }
            
            if (text.StartsWith("- ") || text.StartsWith("* ") || text.StartsWith("1. "))
            {
                return "文本 | Markdown列表";
            }
            
            if (text.Contains("```"))
            {
                return "文本 | 代码";
            }
            
            var codePatterns = new[] { "function ", "def ", "class ", "import ", "using ", "var ", "let ", "const ", "if (", "for (", "while (" };
            if (codePatterns.Any(p => text.Contains(p)))
            {
                return "文本 | 代码";
            }
            
            if (text.Length > 500)
            {
                return "文本 | 长文本";
            }
            
            return "文本";
        }
    }
}
