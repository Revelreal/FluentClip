using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using FluentClip.Models;
using FluentClip.Services;

namespace FluentClip.Factories;

public class ClipboardItemFactory
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff" };
    private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
    private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };

    public static ClipboardItem CreateFromText(string text, string? sourceFilePath = null)
    {
        return new ClipboardItem
        {
            ItemType = ClipboardItemType.Text,
            TextContent = text,
            FilePaths = sourceFilePath != null ? new[] { sourceFilePath } : Array.Empty<string>(),
            Timestamp = DateTime.Now
        };
    }

    public static ClipboardItem CreateFromImage(BitmapSource image, string? sourceFilePath = null)
    {
        return new ClipboardItem
        {
            ItemType = ClipboardItemType.Image,
            ImageContent = image,
            Thumbnail = image,
            FilePaths = sourceFilePath != null ? new[] { sourceFilePath } : Array.Empty<string>(),
            Timestamp = DateTime.Now
        };
    }

    public static ClipboardItem CreateFromFile(string filePath, int thumbnailWidth = 96, int thumbnailHeight = 96)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;

        if (Directory.Exists(filePath))
        {
            return CreateFolderItem(filePath);
        }

        if (ImageExtensions.Contains(ext))
        {
            return CreateImageItem(filePath);
        }

        return CreateGenericFileItem(filePath, thumbnailWidth, thumbnailHeight);
    }

    private static ClipboardItem CreateFolderItem(string folderPath)
    {
        var thumbnail = ThumbnailHelper.GenerateThumbnail(folderPath, 96, 96);

        return new ClipboardItem
        {
            ItemType = ClipboardItemType.File,
            FilePaths = new[] { folderPath },
            Thumbnail = thumbnail,
            Timestamp = DateTime.Now
        };
    }

    private static ClipboardItem CreateImageItem(string imagePath)
    {
        BitmapImage? bitmap = null;
        try
        {
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
        }
        catch { }

        return new ClipboardItem
        {
            ItemType = ClipboardItemType.Image,
            ImageContent = bitmap,
            Thumbnail = bitmap,
            FilePaths = new[] { imagePath },
            Timestamp = DateTime.Now
        };
    }

    private static ClipboardItem CreateGenericFileItem(string filePath, int thumbnailWidth, int thumbnailHeight)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        BitmapSource? thumbnail = null;

        if (AudioExtensions.Contains(ext))
        {
            var metadata = ThumbnailHelper.ExtractAudioMetadata(filePath);
            if (metadata.HasValue)
            {
                thumbnail = metadata.Value.AlbumArt;
            }
        }

        thumbnail ??= ThumbnailHelper.GenerateThumbnail(filePath, thumbnailWidth, thumbnailHeight);

        return new ClipboardItem
        {
            ItemType = ClipboardItemType.File,
            FilePaths = new[] { filePath },
            Thumbnail = thumbnail,
            Timestamp = DateTime.Now
        };
    }

    public static ClipboardItemType GetItemType(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;

        if (Directory.Exists(filePath))
            return ClipboardItemType.File;

        if (ImageExtensions.Contains(ext))
            return ClipboardItemType.Image;

        if (VideoExtensions.Contains(ext) || AudioExtensions.Contains(ext))
            return ClipboardItemType.File;

        return ClipboardItemType.File;
    }
}
