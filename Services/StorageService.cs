using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentClip.Models;

namespace FluentClip.Services;

public class StorageService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FluentClip");

    private static readonly string StagingDataPath = Path.Combine(AppDataPath, "staging_data.json");
    private static readonly string ChatHistoryPath = Path.Combine(AppDataPath, "chat_history.json");
    private static readonly string AppInfoPath = Path.Combine(AppDataPath, "app_info.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static StorageService? _instance;
    public static StorageService Instance => _instance ??= new StorageService();

    public string DeviceId { get; private set; } = "";
    public string DeviceName { get; private set; } = "";

    private StorageService()
    {
        EnsureDirectoryExists();
        LoadAppInfo();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    private void LoadAppInfo()
    {
        try
        {
            if (File.Exists(AppInfoPath))
            {
                var json = File.ReadAllText(AppInfoPath);
                var appData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                if (appData != null)
                {
                    DeviceId = appData.DeviceId;
                    DeviceName = appData.DeviceName;
                    return;
                }
            }
        }
        catch { }

        var newAppData = new AppData();
        DeviceId = newAppData.DeviceId;
        DeviceName = newAppData.DeviceName;
        SaveAppInfo(newAppData);
    }

    private void SaveAppInfo(AppData appData)
    {
        try
        {
            var json = JsonSerializer.Serialize(appData, JsonOptions);
            File.WriteAllText(AppInfoPath, json);
        }
        catch { }
    }

    public StagingData LoadStagingData()
    {
        try
        {
            if (File.Exists(StagingDataPath))
            {
                var json = File.ReadAllText(StagingDataPath);
                return JsonSerializer.Deserialize<StagingData>(json, JsonOptions) ?? new StagingData();
            }
        }
        catch { }
        return new StagingData();
    }

    public void SaveStagingData(StagingData data)
    {
        try
        {
            data.LastUpdated = DateTime.Now;
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(StagingDataPath, json);
        }
        catch { }
    }

    public ChatHistoryData LoadChatHistory()
    {
        try
        {
            if (File.Exists(ChatHistoryPath))
            {
                var json = File.ReadAllText(ChatHistoryPath);
                return JsonSerializer.Deserialize<ChatHistoryData>(json, JsonOptions) ?? new ChatHistoryData();
            }
        }
        catch { }
        return new ChatHistoryData();
    }

    public void SaveChatHistory(ChatHistoryData data)
    {
        try
        {
            data.LastUpdated = DateTime.Now;
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(ChatHistoryPath, json);
        }
        catch { }
    }

    public List<StagingItem> ValidateAndCleanStagingItems(List<StagingItem> items)
    {
        var validItems = new List<StagingItem>();
        var removedCount = 0;

        foreach (var item in items)
        {
            if (item.ItemType == ClipboardItemType.Text)
            {
                validItems.Add(item);
                continue;
            }

            if (item.ItemType == ClipboardItemType.File || item.ItemType == ClipboardItemType.Image)
            {
                var allPathsValid = true;
                var cleanedPaths = new List<string>();

                foreach (var path in item.FilePaths)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        cleanedPaths.Add(path);
                    }
                    else
                    {
                        allPathsValid = false;
                    }
                }

                if (cleanedPaths.Count > 0)
                {
                    item.FilePaths = cleanedPaths;
                    validItems.Add(item);
                }
                else
                {
                    removedCount++;
                }
            }
        }

        if (removedCount > 0)
        {
            Log($"[INFO] 已清理 {removedCount} 个无效的暂存区项（文件不存在）");
        }

        return validItems;
    }

    private void Log(string message)
    {
        try
        {
            var logDir = Path.Combine(AppDataPath, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"storage_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
        catch { }
    }

    public void ClearAllData()
    {
        try
        {
            if (File.Exists(StagingDataPath))
                File.Delete(StagingDataPath);
            if (File.Exists(ChatHistoryPath))
                File.Delete(ChatHistoryPath);
            Log("[INFO] 已清除所有持久化数据");
        }
        catch { }
    }

    public static string GetAppDataPath() => AppDataPath;
}
