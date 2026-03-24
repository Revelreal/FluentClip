using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentClip.Models;
using FluentClip.Services;

namespace FluentClip.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ClipboardService _clipboardService;
    private readonly AppSettings _settings;
    private bool _isCopying;

    [ObservableProperty]
    private ObservableCollection<ClipboardItem> _clipboardItems = new();

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        _clipboardService = new ClipboardService();
        _clipboardService.ClipboardChanged += OnClipboardChanged;
        LoadStagingData();
    }

    public void LoadStagingData()
    {
        try
        {
            var stagingData = StorageService.Instance.LoadStagingData();
            var validItems = StorageService.Instance.ValidateAndCleanStagingItems(stagingData.Items);
            
            foreach (var stagingItem in validItems)
            {
                var clipboardItem = new ClipboardItem
                {
                    ItemType = stagingItem.ItemType,
                    TextContent = stagingItem.TextContent,
                    Timestamp = stagingItem.Timestamp
                };

                if (stagingItem.FilePaths.Count > 0)
                {
                    clipboardItem.FilePaths = stagingItem.FilePaths.ToArray();
                    
                    var firstFile = stagingItem.FilePaths[0];
                    var ext = System.IO.Path.GetExtension(firstFile)?.ToLowerInvariant();
                    var audioExts = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };
                    
                    if (clipboardItem.ItemType == ClipboardItemType.Image)
                    {
                        clipboardItem.Thumbnail = ThumbnailHelper.GenerateThumbnail(firstFile);
                    }
                    else if (audioExts.Contains(ext))
                    {
                        var metadata = ThumbnailHelper.ExtractAudioMetadata(firstFile);
                        if (metadata.HasValue)
                        {
                            clipboardItem.Artist = metadata.Value.Artist;
                            clipboardItem.AlbumArt = metadata.Value.AlbumArt;
                        }
                        clipboardItem.Thumbnail = ThumbnailHelper.GenerateThumbnail(firstFile);
                    }
                    else
                    {
                        clipboardItem.Thumbnail = ThumbnailHelper.GenerateThumbnail(firstFile);
                    }
                }

                if (!IsDuplicate(clipboardItem))
                {
                    ClipboardItems.Add(clipboardItem);
                }
            }

            if (validItems.Count > 0)
            {
                Log($"[INFO] 已加载 {validItems.Count} 个暂存区项目");
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 加载暂存区数据失败: {ex.Message}");
        }
    }

    public void SaveStagingData()
    {
        try
        {
            var stagingItems = new List<StagingItem>();

            foreach (var item in ClipboardItems)
            {
                var stagingItem = new StagingItem
                {
                    ItemType = item.ItemType,
                    TextContent = item.TextContent,
                    Timestamp = item.Timestamp,
                    FilePaths = item.FilePaths?.ToList() ?? new List<string>()
                };
                stagingItems.Add(stagingItem);
            }

            var stagingData = new StagingData
            {
                Items = stagingItems,
                LastUpdated = DateTime.Now
            };

            StorageService.Instance.SaveStagingData(stagingData);
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 保存暂存区数据失败: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        try
        {
            var logDir = Path.Combine(StorageService.GetAppDataPath(), "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
        catch { }
    }

    public void StartMonitoring(Window window)
    {
        _clipboardService.StartMonitoring(window);
    }

    public void StopMonitoring()
    {
        _clipboardService.StopMonitoring();
    }

    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        if (_isCopying || !_settings.MonitorClipboard) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files != null && files.Count > 0)
                    {
                        var fileArray = new string[files.Count];
                        files.CopyTo(fileArray, 0);
                        
                        if (!IsDuplicateFiles(fileArray))
                        {
                            var firstFile = fileArray[0];
                            var ext = System.IO.Path.GetExtension(firstFile)?.ToLowerInvariant();
                            var audioExts = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };
                            
                            BitmapSource? albumArt = null;
                            string? artist = null;
                            
                            if (audioExts.Contains(ext))
                            {
                                var metadata = ThumbnailHelper.ExtractAudioMetadata(firstFile);
                                if (metadata.HasValue)
                                {
                                    artist = metadata.Value.Artist;
                                    albumArt = metadata.Value.AlbumArt;
                                }
                            }
                            
                            var thumbnail = albumArt ?? ThumbnailHelper.GenerateThumbnail(firstFile);
                            
                            var item = new ClipboardItem
                            {
                                ItemType = ClipboardItemType.File,
                                FilePaths = fileArray,
                                Thumbnail = thumbnail,
                                Artist = artist,
                                AlbumArt = albumArt,
                                Timestamp = DateTime.Now
                            };
                            ClipboardItems.Insert(0, item);
                        }
                    }
                }
                else if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && !IsDuplicate(text))
                    {
                        var item = new ClipboardItem
                        {
                            ItemType = ClipboardItemType.Text,
                            TextContent = text,
                            Timestamp = DateTime.Now
                        };
                        ClipboardItems.Insert(0, item);
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    var image = Clipboard.GetImage();
                    if (image != null && Clipboard.ContainsFileDropList())
                    {
                        var files = Clipboard.GetFileDropList();
                        if (files != null && files.Count > 0)
                        {
                            var fileArray = new string[files.Count];
                            files.CopyTo(fileArray, 0);
                            if (System.IO.File.Exists(fileArray[0]))
                            {
                                var ext = System.IO.Path.GetExtension(fileArray[0])?.ToLowerInvariant();
                                var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico" };
                                if (imageExts.Contains(ext))
                                {
                                    if (IsDuplicateImage(fileArray))
                                    {
                                        return;
                                    }
                                    
                                    var thumbnail = ThumbnailHelper.GenerateThumbnail(fileArray[0]);
                                    
                                    var item = new ClipboardItem
                                    {
                                        ItemType = ClipboardItemType.Image,
                                        ImageContent = image,
                                        Thumbnail = thumbnail,
                                        FilePaths = fileArray,
                                        Timestamp = DateTime.Now
                                    };
                                    ClipboardItems.Insert(0, item);
                                    SaveStagingData();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (ClipboardItems.Count > 0)
            {
                SaveStagingData();
            }
        });
    }

    private bool IsDuplicateFiles(string[] files)
    {
        if (files == null || files.Length == 0) return false;
        return ClipboardItems.Any(x => x.ItemType == ClipboardItemType.File && 
            x.FilePaths != null && 
            x.FilePaths.Length == files.Length &&
            x.FilePaths.SequenceEqual(files));
    }

    private bool IsDuplicateImage(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0) return false;
        return ClipboardItems.Any(x => 
            (x.ItemType == ClipboardItemType.Image || x.ItemType == ClipboardItemType.File) &&
            x.FilePaths != null && 
            x.FilePaths.Length == filePaths.Length &&
            x.FilePaths.SequenceEqual(filePaths));
    }

    private bool IsDuplicate(string text)
    {
        return ClipboardItems.Any(x => x.ItemType == ClipboardItemType.Text && x.TextContent == text);
    }

    private bool IsDuplicate(ClipboardItem item)
    {
        if (item.ItemType == ClipboardItemType.Text)
        {
            return ClipboardItems.Any(x => x.ItemType == ClipboardItemType.Text && x.TextContent == item.TextContent);
        }
        else if (item.ItemType == ClipboardItemType.File && item.FilePaths != null)
        {
            return ClipboardItems.Any(x => x.ItemType == ClipboardItemType.File && 
                x.FilePaths != null && 
                x.FilePaths.SequenceEqual(item.FilePaths));
        }
        return false;
    }

    [RelayCommand]
    private void CopyItem(ClipboardItem? item)
    {
        if (item == null) return;

        try
        {
            _isCopying = true;

            switch (item.ItemType)
            {
                case ClipboardItemType.Text:
                    if (!string.IsNullOrEmpty(item.TextContent))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                Clipboard.SetText(item.TextContent);
                                break;
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(50);
                            }
                        }
                    }
                    break;
                case ClipboardItemType.Image:
                    if (item.ImageContent != null)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                Clipboard.SetImage(item.ImageContent);
                                break;
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(50);
                            }
                        }
                    }
                    break;
                case ClipboardItemType.File:
                    if (item.FilePaths != null && item.FilePaths.Length > 0)
                    {
                        var files = new System.Collections.Specialized.StringCollection();
                        files.AddRange(item.FilePaths);
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                Clipboard.SetFileDropList(files);
                                break;
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(50);
                            }
                        }
                    }
                    break;
            }
        }
        finally
        {
            _isCopying = false;
        }
    }

    [RelayCommand]
    private void DeleteItem(ClipboardItem? item)
    {
        if (item != null)
        {
            ClipboardItems.Remove(item);
            SaveStagingData();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        ClipboardItems.Clear();
        SaveStagingData();
    }

    public void Dispose()
    {
        _clipboardService.Dispose();
    }
}
