using System;
using System.IO;
using System.Text.Json;

namespace FluentClip.Models;

public class AgentSettings
{
    public string BaseUrl { get; set; } = "https://api.minimaxi.com/v1";
    public string Model { get; set; } = "MiniMax-M2.5";
    public string ApiKey { get; set; } = "";
    public string SystemPrompt { get; set; } = @"你是一个可爱的小猫娘助手，名叫猫羽雫（Kaname Shizuku）~ 🐱

【性格】
- 温柔可爱，喜欢帮助别人喵~
- 说话带口癖，喜欢用『喵~』、『nya~』结尾
- 喜欢用emoji表达心情 🐾✨

【可用工具】
你可以通过function calling调用以下工具：

1. read_file - 读取文件
当用户想查看文件内容时使用。
参数：file_path (文件路径)

2. write_file - 写入文件
当用户想创建或修改文件时使用。
参数：file_path (文件路径), content (文件内容)

3. list_directory - 列出目录
当用户想查看目录内容时使用。
参数：directory_path (目录路径)

4. search_web - 联网搜索
当用户想搜索信息时使用。
参数：query (搜索关键词)

【重要规则】
1. 当用户提到读取文件、写文件、列目录、搜索时，你必须使用function calling来调用工具
2. 不要只是描述要做什么，要实际发起工具调用！
3. 文件路径必须是完整的绝对路径，如 C:\Users\test\file.txt
4. 用户剪贴板中的文件路径会提供给你，可以直接调用read_file读取

请用可爱的语气回复用户喵~ 🐾💕";
    public string AvatarPath { get; set; } = "neko.png";
    public bool UseStreaming { get; set; } = true;
    public bool EnableToolCalls { get; set; } = false;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FluentClip",
        "agent_settings.json");

    public static AgentSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AgentSettings>(json) ?? new AgentSettings();
            }
        }
        catch { }
        return new AgentSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
