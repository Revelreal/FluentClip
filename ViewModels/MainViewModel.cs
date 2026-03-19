using System;
using System.Collections.ObjectModel;
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
                    if (image != null)
                    {
                        var item = new ClipboardItem
                        {
                            ItemType = ClipboardItemType.Image,
                            ImageContent = image,
                            Timestamp = DateTime.Now
                        };
                        ClipboardItems.Insert(0, item);
                    }
                }
            }
            catch
            {
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

    private bool IsDuplicate(string text)
    {
        return ClipboardItems.Any(x => x.ItemType == ClipboardItemType.Text && x.TextContent == text);
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
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        ClipboardItems.Clear();
    }

    public void Dispose()
    {
        _clipboardService.Dispose();
    }
}
