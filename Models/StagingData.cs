using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluentClip.Models;

public class StagingData
{
    public List<StagingItem> Items { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class StagingItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ClipboardItemType ItemType { get; set; }
    public string? TextContent { get; set; }
    public List<string> FilePaths { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsPinned { get; set; } = false;
    public string? SourceDeviceId { get; set; }
}

public class ChatHistoryData
{
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? DeviceId { get; set; }
}

public class AppData
{
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceName { get; set; } = Environment.MachineName;
    public DateTime LastSyncTime { get; set; } = DateTime.Now;
}
