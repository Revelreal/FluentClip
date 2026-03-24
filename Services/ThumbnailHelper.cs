using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TagLib;

namespace FluentClip.Services;

public static class ThumbnailHelper
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static BitmapSource? GenerateThumbnailFromImage(BitmapSource image, int width = 96, int height = 96)
    {
        try
        {
            var scaleX = width / image.Width;
            var scaleY = height / image.Height;
            var scale = Math.Min(scaleX, scaleY);
            
            var thumbnail = new TransformedBitmap(image, new ScaleTransform(scale, scale));
            return thumbnail;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? GenerateThumbnail(string filePath, int width = 128, int height = 128)
    {
        try
        {
            if (Directory.Exists(filePath))
            {
                return ExtractFolderIcon(filePath, width, height);
            }

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            
            return ext switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" => LoadImageThumbnail(filePath, width, height),
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" => LoadVideoThumbnail(filePath, width, height),
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" => LoadAudioThumbnail(filePath, width, height),
                _ => ExtractSystemIconTransparent(filePath, width, height)
            };
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? ExtractFolderIcon(string filePath, int width, int height)
    {
        try
        {
            var shFileInfo = new SHFILEINFO();
            var result = SHGetFileInfo(filePath, FILE_ATTRIBUTE_DIRECTORY, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), 
                SHGFI_ICON | SHGFI_LARGEICON);

            if (result != IntPtr.Zero && shFileInfo.hIcon != IntPtr.Zero)
            {
                var icon = Icon.FromHandle(shFileInfo.hIcon);
                using var bitmap = icon.ToBitmap();
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(width, height));
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DestroyIcon(shFileInfo.hIcon);
                }
            }
        }
        catch { }

        return null;
    }

    private static BitmapSource? LoadImageThumbnail(string filePath, int width, int height)
    {
        try
        {
            var fullBitmap = new BitmapImage();
            fullBitmap.BeginInit();
            fullBitmap.UriSource = new Uri(filePath);
            fullBitmap.CacheOption = BitmapCacheOption.OnLoad;
            fullBitmap.EndInit();
            fullBitmap.Freeze();

            int fullWidth = fullBitmap.PixelWidth;
            int fullHeight = fullBitmap.PixelHeight;

            int sourceX = 0, sourceY = 0, sourceWidth = fullWidth, sourceHeight = fullHeight;
            
            double sourceAspect = (double)fullWidth / fullHeight;
            double targetAspect = (double)width / height;

            if (sourceAspect > targetAspect)
            {
                sourceWidth = (int)(fullHeight * targetAspect);
                sourceX = (fullWidth - sourceWidth) / 2;
            }
            else if (sourceAspect < targetAspect)
            {
                sourceHeight = (int)(fullWidth / targetAspect);
                sourceY = (fullHeight - sourceHeight) / 2;
            }

            var croppedBitmap = new CroppedBitmap(fullBitmap, new Int32Rect(sourceX, sourceY, sourceWidth, sourceHeight));
            
            var resizedBitmap = new BitmapImage();
            resizedBitmap.BeginInit();
            resizedBitmap.StreamSource = BitmapSourceToStream(croppedBitmap);
            resizedBitmap.DecodePixelWidth = width;
            resizedBitmap.DecodePixelHeight = height;
            resizedBitmap.CacheOption = BitmapCacheOption.OnLoad;
            resizedBitmap.EndInit();
            resizedBitmap.Freeze();
            
            return resizedBitmap;
        }
        catch
        {
            return null;
        }
    }

    private static MemoryStream? BitmapSourceToStream(BitmapSource source)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? LoadVideoThumbnail(string filePath, int width, int height)
    {
        return ExtractSystemIconTransparent(filePath, width, height);
    }

    private static BitmapSource? LoadAudioThumbnail(string filePath, int width, int height)
    {
        var metadata = ExtractAudioMetadata(filePath);
        if (metadata.HasValue && metadata.Value.AlbumArt != null)
        {
            return metadata.Value.AlbumArt;
        }
        return ExtractSystemIconTransparent(filePath, width, height);
    }

    private static BitmapSource? ExtractSystemIconTransparent(string filePath, int width, int height)
    {
        try
        {
            var shFileInfo = new SHFILEINFO();
            var result = SHGetFileInfo(filePath, 0, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), 
                SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

            if (result != IntPtr.Zero && shFileInfo.hIcon != IntPtr.Zero)
            {
                var icon = Icon.FromHandle(shFileInfo.hIcon);
                using var bitmap = icon.ToBitmap();
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(width, height));
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DestroyIcon(shFileInfo.hIcon);
                }
            }
        }
        catch { }

        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, 
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    public static (string? Artist, string? Album, BitmapSource? AlbumArt)? ExtractAudioMetadata(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            
            string? artist = null;
            string? album = null;
            BitmapSource? albumArt = null;

            if (!string.IsNullOrEmpty(file.Tag.FirstPerformer))
                artist = file.Tag.FirstPerformer;
            else if (!string.IsNullOrEmpty(file.Tag.FirstAlbumArtist))
                artist = file.Tag.FirstAlbumArtist;

            if (!string.IsNullOrEmpty(file.Tag.Album))
                album = file.Tag.Album;

            if (file.Tag.Pictures.Length > 0)
            {
                var picture = file.Tag.Pictures[0];
                using var stream = new MemoryStream(picture.Data.Data);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 128;
                bitmap.DecodePixelHeight = 128;
                bitmap.EndInit();
                bitmap.Freeze();
                albumArt = bitmap;
            }

            return (artist, album, albumArt);
        }
        catch
        {
            return null;
        }
    }
}
