using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentClip.Models;

namespace FluentClip.Services;

public class AgentService
{
    private readonly AgentSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly List<Message> _conversationHistory = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _logFilePath;
    private readonly WebScraperService _webScraper;
    private readonly WeatherService _weatherService;
    private readonly string[] _highRiskPatterns = new[]
    {
        @"rm\s+-rf", @"del\s+/[fq]", @"format", @"diskpart", @"reg\s+delete",
        @"shutdown", @"restart", @"stop-computer", @"kill\s+", @"taskkill",
        @"remove-item\s+-recurse", @"rmdir", @"chmod\s+777", @"icacls\s+.*\/grant",
        @"net\s+user", @"net\s+localgroup", @"powershell.*-enc", @"invoke-expression",
        @"downloadstring", @"downloadfile", @"webclient", @"invoke-webrequest.*-outfile",
        @"set-executionpolicy", @"new-service", @"new-scheduledtask"
    };

    public Func<IEnumerable<ClipboardItem>>? GetStagingFilesCallback { get; set; }
    public Func<IEnumerable<string>>? GetAiWorkFolderFilesCallback { get; set; }
    public string AiWorkFolder => _settings.AiWorkFolder;
    public bool EnableShellExecution => _settings.EnableShellExecution;

    public int MessageCount => _conversationHistory.Count;
    public int TotalTokens { get; private set; }

    private const int MaxTokens = 100000;

    public void LoadChatHistory()
    {
        try
        {
            var chatData = StorageService.Instance.LoadChatHistory();
            _conversationHistory.Clear();

            foreach (var chatMsg in chatData.Messages)
            {
                _conversationHistory.Add(new Message
                {
                    Role = chatMsg.Role,
                    Content = chatMsg.Content
                });
            }

            UpdateTokenCount();

            if (chatData.Messages.Count > 0)
            {
                Log($"[INFO] 已加载 {chatData.Messages.Count} 条聊天历史");
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 加载聊天历史失败: {ex.Message}");
        }
    }

    public void SaveChatHistory()
    {
        try
        {
            var chatMessages = new List<ChatMessage>();

            foreach (var msg in _conversationHistory)
            {
                chatMessages.Add(new ChatMessage
                {
                    Role = msg.Role,
                    Content = msg.Content,
                    Timestamp = DateTime.Now,
                    DeviceId = StorageService.Instance.DeviceId
                });
            }

            var chatData = new ChatHistoryData
            {
                Messages = chatMessages,
                LastUpdated = DateTime.Now
            };

            StorageService.Instance.SaveChatHistory(chatData);
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 保存聊天历史失败: {ex.Message}");
        }
    }

    public List<ChatMessage> GetConversationHistory()
    {
        return _conversationHistory.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = DateTime.Now,
            DeviceId = StorageService.Instance.DeviceId
        }).ToList();
    }

    public double TokenUsagePercentage => MaxTokens > 0 ? (TotalTokens * 100.0 / MaxTokens) : 0;
    public int MaxTokenLimit => MaxTokens;

    public event Action<string>? OnStreamingResponse;
    public event Action? OnComplete;
    public event Action<string>? OnError;
    public event Action<string>? OnToolCallStart;
    public event Action? OnToolCallComplete;
    public event Action<int, int>? OnTokenUsageUpdated;
    public event Action<string>? OnContextSummarized;
    public event Func<string, Task<bool>>? OnConfirmHighRiskCommand;
    public event Action<string>? OnShellOutput;
    public event Action<string>? OnShellError;
    public event Action? OnShellComplete;

    public AgentService(AgentSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _webScraper = new WebScraperService();
        _weatherService = new WeatherService();
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        }
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        // 初始化日志文件路径
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, $"agent_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        Log("AgentService initialized");
    }

    private void Log(string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            Console.WriteLine(logEntry);
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 日志写入失败: {ex.Message}");
        }
    }

    private string ResolveCommonPath(string path)
    {
        // 处理常见目录名
        var lowerPath = path.ToLower();
        
        if (lowerPath.Contains("桌面") || lowerPath == "desktop")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }
        else if (lowerPath.Contains("文档") || lowerPath == "documents")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        else if (lowerPath.Contains("下载") || lowerPath == "downloads")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        }
        else if (lowerPath.Contains("图片") || lowerPath == "pictures")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
        else if (lowerPath.Contains("音乐") || lowerPath == "music")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        }
        else if (lowerPath.Contains("视频") || lowerPath == "videos")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }
        else if (lowerPath == "用户" || lowerPath == "user" || lowerPath == "home")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        
        return path;
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
        TotalTokens = 0;
    }

    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int chineseChars = 0;
        int otherChars = 0;
        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
                chineseChars++;
            else
                otherChars++;
        }
        return chineseChars / 2 + (otherChars + 3) / 4;
    }

    public bool CheckAndClearContextIfFull()
    {
        if (TotalTokens >= MaxTokens)
        {
            _conversationHistory.Clear();
            TotalTokens = 0;
            return true;
        }
        return false;
    }

    public bool ShouldSummarize => TotalTokens >= MaxTokens * 0.95;

    public async Task<string> SummarizeContextAsync()
    {
        if (_conversationHistory.Count == 0)
        {
            return "没有上下文需要总结喵~";
        }

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            return "API Key未配置，无法进行总结喵~";
        }

        Log("[DEBUG] 开始总结上下文...");

        var summaryPrompt = @"请简洁地总结以上对话的要点，保留关键信息、用户需求和重要结论。直接给出总结内容，不需要开场白。";

        var messagesToSummarize = new List<Message>();
        var systemContent = BuildSystemPrompt();
        if (!string.IsNullOrEmpty(systemContent))
        {
            messagesToSummarize.Add(new Message { Role = "system", Content = systemContent });
        }
        messagesToSummarize.AddRange(_conversationHistory);
        messagesToSummarize.Add(new Message { Role = "user", Content = summaryPrompt });

        var requestBody = new ChatRequest
        {
            Model = _settings.Model,
            Messages = messagesToSummarize,
            Stream = false
        };

        var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log($"[ERROR] 总结请求失败: {errorContent}");
                return $"总结失败: {errorContent}";
            }

            var responseData = await response.Content.ReadAsStringAsync();
            
            using var jsonDoc = JsonDocument.Parse(responseData);
            var choices = jsonDoc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var contentElement))
                {
                    var summary = contentElement.GetString();
                    if (!string.IsNullOrEmpty(summary))
                    {
                        Log($"[DEBUG] 总结完成，长度: {summary.Length}");
                        ApplySummary(summary);
                        OnContextSummarized?.Invoke(summary);
                        return summary;
                    }
                }
            }

            return "无法生成总结喵~";
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 总结上下文时出错: {ex.Message}");
            return $"总结出错: {ex.Message}";
        }
    }

    private void ApplySummary(string summary)
    {
        _conversationHistory.Clear();
        
        var summaryMessage = $"【对话总结】\n{summary}";
        _conversationHistory.Add(new Message { Role = "system", Content = summaryMessage });
        
        UpdateTokenCount();
        Log($"[DEBUG] 总结已应用，剩余token: {TotalTokens}");
    }

    private void UpdateTokenCount()
    {
        int tokens = 0;
        foreach (var msg in _conversationHistory)
        {
            if (!string.IsNullOrEmpty(msg.Content))
            {
                tokens += EstimateTokens(msg.Content);
            }
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    if (!string.IsNullOrEmpty(tc.Function?.Name))
                        tokens += EstimateTokens(tc.Function.Name);
                    if (!string.IsNullOrEmpty(tc.Function?.Arguments))
                        tokens += EstimateTokens(tc.Function.Arguments);
                }
            }
        }
        TotalTokens = tokens;
    }

    public void AddUserMessage(string content)
    {
        _conversationHistory.Add(new Message { Role = "user", Content = content });
    }

    public void AddAssistantMessage(string content)
    {
        _conversationHistory.Add(new Message { Role = "assistant", Content = content });
    }

    public async Task SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                OnError?.Invoke("请先在设置中配置API Key");
                return;
            }

            var (processedMessage, toolResult) = TryProcessFileOperations(userMessage);
            
            if (toolResult != null)
            {
                OnStreamingResponse?.Invoke(toolResult);
                _conversationHistory.Add(new Message { Role = "assistant", Content = toolResult });
                OnComplete?.Invoke();
                return;
            }
            
            _conversationHistory.Add(new Message { Role = "user", Content = processedMessage });

            var messages = BuildMessages();
            var tools = _settings.EnableToolCalls ? GetToolDefinitions() : null;

            var requestBody = new ChatRequest
            {
                Model = _settings.Model,
                Messages = messages,
                Tools = tools,
                ToolChoice = _settings.EnableToolCalls ? "auto" : null,
                Stream = _settings.UseStreaming
            };

        var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            if (_settings.UseStreaming)
            {
                await SendStreamingRequestAsync(content, cancellationToken);
            }
            else
            {
                await SendNonStreamingRequestAsync(content, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"请求失败: {ex.Message}");
        }
    }

    private (string message, string? toolResult) TryProcessFileOperations(string userMessage)
    {
        var lowerMessage = userMessage.ToLower();
        
        var readFileMatch = Regex.Match(userMessage, @"(?:读取?|read|查看|查看文件|看文件)\s*[:：]?\s*(.+?)(?:\s|$)", RegexOptions.IgnoreCase);
        if (readFileMatch.Success)
        {
            var filePath = readFileMatch.Groups[1].Value.Trim();
            OnToolCallStart?.Invoke("read_file");
            var result = ReadFileContent(filePath);
            var response = $"用户请求读取文件：{filePath}\n\n{result}";
            return (response, result);
        }

        var writeFileMatch = Regex.Match(userMessage, @"(?:写入?|write|创建|生成)\s+(?:文件\s+)?(.+?)\s*(?:内容|是|：|:)\s*([\s\S]+)", RegexOptions.IgnoreCase);
        if (writeFileMatch.Success)
        {
            var filePath = writeFileMatch.Groups[1].Value.Trim();
            var content = writeFileMatch.Groups[2].Value.Trim();
            OnToolCallStart?.Invoke("write_file");
            var result = WriteFileContent(filePath, content);
            var response = $"用户请求写入文件：{filePath}\n\n{result}";
            return (response, result);
        }

        var listDirMatch = Regex.Match(userMessage, @"(?:列出?|list|查看目录)\s*(?:目录\s+)?(.+?)(?:\s|$)", RegexOptions.IgnoreCase);
        if (listDirMatch.Success)
        {
            var dirPath = listDirMatch.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(dirPath) || dirPath == "目录" || dirPath == "dir")
            {
                dirPath = ".";
            }
            OnToolCallStart?.Invoke("list_directory");
            var result = ListDirectory(dirPath);
            var response = $"用户请求列出目录：{dirPath}\n\n{result}";
            return (response, result);
        }

        return (userMessage, null);
    }

    private List<Message> BuildMessages()
    {
        var messages = new List<Message>();

        var systemContent = BuildSystemPrompt();
        if (!string.IsNullOrEmpty(systemContent))
        {
            messages.Add(new Message { Role = "system", Content = systemContent });
        }

        messages.AddRange(_conversationHistory);

        UpdateTokenCount();

        return messages;
    }

    private string BuildSystemPrompt()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(_settings.PersonaPrompt))
        {
            parts.Add(_settings.PersonaPrompt.Trim());
        }

        if (!string.IsNullOrEmpty(_settings.SystemPrompt))
        {
            parts.Add(_settings.SystemPrompt.Trim());
        }

        return string.Join("\n\n", parts);
    }

    private List<ToolDefinition> GetToolDefinitions()
    {
        var tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "read_file",
                    Description = "读取指定路径的文本文件内容。适用于读取代码文件、配置文件、文本文件等。输入必须是完整的绝对路径，如 C:\\Users\\test\\document.txt",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["file_path"] = new PropertySchema 
                            { 
                                Type = "string", 
                                Description = "要读取的文件完整路径（必须使用绝对路径），例如: C:\\Users\\test\\document.txt 或 C:/Users/test/document.txt" 
                            }
                        },
                        Required = new List<string> { "file_path" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "write_file",
                    Description = "将内容写入到指定的文件路径。如果文件已存在，会覆盖原内容。输入必须是完整的绝对路径。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["file_path"] = new PropertySchema 
                            { 
                                Type = "string", 
                                Description = "要写入的文件完整路径（必须使用绝对路径），例如: C:\\Users\\test\\output.txt" 
                            },
                            ["content"] = new PropertySchema 
                            { 
                                Type = "string", 
                                Description = "要写入的文件内容（完整的文本内容）" 
                            }
                        },
                        Required = new List<string> { "file_path", "content" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_directory",
                    Description = "列出指定目录下的文件和文件夹。输入必须是完整的绝对路径。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["directory_path"] = new PropertySchema 
                            { 
                                Type = "string", 
                                Description = "要列出的目录完整路径（必须使用绝对路径），例如: C:\\Users\\test 或 C:/Users/test" 
                            }
                        },
                        Required = new List<string> { "directory_path" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "search_web",
                    Description = "使用必应搜索功能进行网络搜索，获取相关信息。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["query"] = new PropertySchema
                            {
                                Type = "string",
                                Description = "搜索关键词"
                            }
                        },
                        Required = new List<string> { "query" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_staging_files",
                    Description = "列出用户剪贴板暂存区中的所有文件。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                        },
                        Required = new List<string> { }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "get_weather",
                    Description = "查询指定城市的天气信息，包括温度、天气状况、风力等。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["city"] = new PropertySchema
                            {
                                Type = "string",
                                Description = "要查询天气的城市名称，例如：北京、上海、广州、深圳、杭州等"
                            }
                        },
                        Required = new List<string> { "city" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "execute_shell",
                    Description = "执行CMD或PowerShell命令。某些高风险命令（如删除文件、格式化磁盘等）需要用户确认才能执行。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                            ["command"] = new PropertySchema
                            {
                                Type = "string",
                                Description = "要执行的命令，例如：Get-Process、dir、ls 等"
                            },
                            ["shell"] = new PropertySchema
                            {
                                Type = "string",
                                Description = "Shell类型，可选值为 'cmd' 或 'powershell'，默认为 'powershell'"
                            }
                        },
                        Required = new List<string> { "command" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_ai_work_folder",
                    Description = "列出AI专用工作文件夹中的所有文件。这个文件夹中的文件会自动添加到剪贴板暂存区，方便用户快速访问AI生成的文件。",
                    Parameters = new ToolParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, PropertySchema>
                        {
                        },
                        Required = new List<string> { }
                    }
                }
            }
        };
        
        Log($"[DEBUG] GetToolDefinitions: 返回 {tools.Count} 个工具定义");
        return tools;
    }

    private async Task SendStreamingRequestAsync(StringContent content, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions")
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                OnError?.Invoke($"API错误 ({response.StatusCode}): {errorContent}");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var fullResponse = new StringBuilder();
            string? line;
            var toolCalls = new List<ToolCall>();
            
            // 用于累积流式响应中的工具参数
            var currentToolCall = (ToolCall?)null;
            var currentArgsBuffer = new StringBuilder();

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (line.StartsWith("data: ") && line.Length > 6)
                {
                    var data = line.Substring(6);
                    if (data == "[DONE]")
                        {
                            Log("[DEBUG] 收到 [DONE] 信号");
                            
                            // 保存当前累积的工具调用（只有有效的工具调用才保存）
                            if (currentToolCall != null && !string.IsNullOrEmpty(currentToolCall.Function.Name))
                            {
                                currentToolCall.Function.Arguments = currentArgsBuffer.ToString();
                                toolCalls.Add(currentToolCall);
                                Log($"[DEBUG] 保存工具调用: {currentToolCall.Function.Name}");
                            }
                            currentToolCall = null;
                            currentArgsBuffer.Clear();
                            
                            // 处理工具调用
                            if (toolCalls.Count > 0)
                            {
                                Log($"[DEBUG] 处理 {toolCalls.Count} 个工具调用");
                                foreach (var toolCall in toolCalls)
                                {
                                    Log($"[DEBUG] 执行工具: {toolCall.Function.Name}");
                                    var toolResult = await ExecuteToolCallAsync(toolCall.Function.Name, toolCall.Function.Arguments);
                                    Log($"[DEBUG] 工具执行完成，结果: {toolResult.Substring(0, Math.Min(100, toolResult.Length))}...");

                                    // 添加 Assistant 的 tool_call 消息
                                    var toolCallMessage = new Message
                                    {
                                        Role = "assistant",
                                        Content = null,
                                        ToolCalls = new List<ToolCall> { toolCall }
                                    };
                                    _conversationHistory.Add(toolCallMessage);

                                    // 添加工具结果消息
                                    _conversationHistory.Add(new Message
                                    {
                                        Role = "tool",
                                        Content = toolResult,
                                        ToolCallId = toolCall.Id
                                    });

                                    OnToolCallComplete?.Invoke();
                                }

                                Log("[DEBUG] 准备继续请求（包含工具结果）");
                                var continueRequest = new ChatRequest
                                {
                                    Model = _settings.Model,
                                    Messages = BuildMessages(),
                                    Tools = _settings.EnableToolCalls ? GetToolDefinitions() : null,
                                    Stream = true
                                };
                                var continueJson = JsonSerializer.Serialize(continueRequest, _jsonOptions);
                                var continueContent = new StringContent(continueJson, Encoding.UTF8, "application/json");
                                await SendStreamingRequestAsync(continueContent, cancellationToken);
                                return;
                            }
                            Log("[DEBUG] 流式响应结束");
                            break;
                        }

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(data);
                        var choices = jsonDoc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var choice = choices[0];
                            var delta = choice.GetProperty("delta");
                            
                            // 检查 finish_reason，如果是 tool_calls 说明流式响应结束但有工具调用
                            var finishReason = "";
                            if (choice.TryGetProperty("finish_reason", out var finishReasonElement))
                            {
                                finishReason = finishReasonElement.GetString() ?? "";
                            }
                            
                            if (delta.TryGetProperty("tool_calls", out var toolCallsElement))
                            {
                                Log("[DEBUG] 收到 tool_calls");
                                foreach (var toolCallElement in toolCallsElement.EnumerateArray())
                                {
                                    string id = "";
                                    string name = "";
                                    string argsChunk = "";
                                    
                                    if (toolCallElement.TryGetProperty("id", out var idElement))
                                        id = idElement.GetString() ?? "";
                                    
                                    // 如果没有 id，生成一个唯一 id
                                    if (string.IsNullOrEmpty(id))
                                    {
                                        id = $"call_{Guid.NewGuid():N}";
                                        Log($"[DEBUG] 工具调用无id，生成唯一id: {id}");
                                    }
                                    
                                    if (toolCallElement.TryGetProperty("function", out var functionElement))
                                    {
                                        if (functionElement.TryGetProperty("name", out var nameElement))
                                            name = nameElement.GetString() ?? "";
                                        if (functionElement.TryGetProperty("arguments", out var argsElement))
                                            argsChunk = argsElement.GetString() ?? "";
                                    }
                                    
                                    // 如果有 name，说明是新的工具调用开始
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        // 如果是新的工具调用，先保存之前的（只有有效的才保存）
                                        if (currentToolCall != null && currentToolCall.Function.Name != name)
                                        {
                                            if (!string.IsNullOrEmpty(currentToolCall.Function.Name))
                                            {
                                                currentToolCall.Function.Arguments = currentArgsBuffer.ToString();
                                                toolCalls.Add(currentToolCall);
                                                Log($"[DEBUG] 保存之前的工具调用: {currentToolCall.Function.Name}");
                                            }
                                            currentArgsBuffer.Clear();
                                        }
                                        
                                        // 开始新的工具调用
                                        if (currentToolCall == null || currentToolCall.Function.Name != name)
                                        {
                                            OnToolCallStart?.Invoke(name);
                                            currentToolCall = new ToolCall
                                            {
                                                Id = id,
                                                Type = "function",
                                                Function = new FunctionCall
                                                {
                                                    Name = name,
                                                    Arguments = argsChunk
                                                }
                                            };
                                            currentArgsBuffer.Clear();
                                            currentArgsBuffer.Append(argsChunk);
                                            Log($"[DEBUG] 开始新工具调用: {name}, 参数: {argsChunk}");
                                        }
                                        else
                                        {
                                            // 累积参数片段
                                            currentArgsBuffer.Append(argsChunk);
                                            Log($"[DEBUG] 累积参数片段: {argsChunk}");
                                        }
                                    }
                                    // 如果没有 name 但有 arguments，说明是同一个工具调用的参数续接
                                    else if (!string.IsNullOrEmpty(argsChunk) && currentToolCall != null)
                                    {
                                        currentArgsBuffer.Append(argsChunk);
                                        Log($"[DEBUG] 续接参数片段: {argsChunk}");
                                    }
                                }
                                
                                // 检查是否需要处理工具调用（finish_reason 为 tool_calls 时）
                                if (finishReason == "tool_calls")
                                {
                                    Log("[DEBUG] finish_reason 为 tool_calls，准备处理工具调用");
                                    
                                    // 保存当前累积的工具调用
                                    if (currentToolCall != null && !string.IsNullOrEmpty(currentToolCall.Function.Name))
                                    {
                                        currentToolCall.Function.Arguments = currentArgsBuffer.ToString();
                                        toolCalls.Add(currentToolCall);
                                        Log($"[DEBUG] 保存工具调用: {currentToolCall.Function.Name}");
                                    }
                                    currentToolCall = null;
                                    currentArgsBuffer.Clear();
                                    
                                    // 处理工具调用
                                    if (toolCalls.Count > 0)
                                    {
                                        Log($"[DEBUG] 处理 {toolCalls.Count} 个工具调用");
                                        foreach (var toolCall in toolCalls)
                                        {
                                            Log($"[DEBUG] 执行工具: {toolCall.Function.Name}");
                                            var toolResult = await ExecuteToolCallAsync(toolCall.Function.Name, toolCall.Function.Arguments);
                                            Log($"[DEBUG] 工具执行完成");

                                            var toolCallMessage = new Message
                                            {
                                                Role = "assistant",
                                                Content = null,
                                                ToolCalls = new List<ToolCall> { toolCall }
                                            };
                                            _conversationHistory.Add(toolCallMessage);

                                            _conversationHistory.Add(new Message
                                            {
                                                Role = "tool",
                                                Content = toolResult,
                                                ToolCallId = toolCall.Id
                                            });

                                            OnToolCallComplete?.Invoke();
                                        }

                                        Log("[DEBUG] 准备继续请求（包含工具结果）");
                                        var continueRequest = new ChatRequest
                                        {
                                            Model = _settings.Model,
                                            Messages = BuildMessages(),
                                            Tools = _settings.EnableToolCalls ? GetToolDefinitions() : null,
                                            Stream = true
                                        };
                                        var continueJson = JsonSerializer.Serialize(continueRequest, _jsonOptions);
                                        var continueContent = new StringContent(continueJson, Encoding.UTF8, "application/json");
                                        await SendStreamingRequestAsync(continueContent, cancellationToken);
                                        return;
                                    }
                                }
                            }
                            else if (delta.TryGetProperty("content", out var contentElement))
                            {
                                var chunk = contentElement.GetString();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    fullResponse.Append(chunk);
                                    OnStreamingResponse?.Invoke(chunk);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] 解析流式响应失败: {ex.Message}");
                    }
                }
            }

            if (fullResponse.Length > 0)
            {
                Log($"[DEBUG] 流式响应完成，内容长度: {fullResponse.Length}");
                _conversationHistory.Add(new Message { Role = "assistant", Content = fullResponse.ToString() });
                Log($"[DEBUG] 已添加 Assistant 消息到对话历史");
            }
            else
            {
                Log($"[DEBUG] 流式响应完成，无内容");
            }

            Log($"[DEBUG] 准备调用 OnComplete");
            OnComplete?.Invoke();
            Log($"[DEBUG] OnComplete 已调用");
        }

    private async Task<string> ExecuteToolCallAsync(string toolName, string arguments)
    {
        try
        {
            Log($"[DEBUG] 开始执行工具: {toolName}");
            Log($"[DEBUG] 工具参数: {arguments}");
            
            if (string.IsNullOrEmpty(toolName))
            {
                Log("[ERROR] 工具名称为空");
                return "工具名称为空";
            }
            
            if (string.IsNullOrEmpty(arguments))
            {
                Log("[ERROR] 工具参数为空");
                return "工具参数为空";
            }

            // 尝试解析参数
            Dictionary<string, JsonElement>? args = null;
            try
            {
                args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] 工具参数解析失败: {ex.Message}");
                return $"工具参数解析失败: {ex.Message}";
            }

            if (args == null)
            {
                Log("[ERROR] 工具参数解析失败");
                return "工具参数解析失败，参数格式不正确";
            }

            Log($"[DEBUG] 工具参数解析成功，参数数量: {args.Count}");

            switch (toolName)
            {
                case "read_file":
                    if (args.TryGetValue("file_path", out var filePath))
                    {
                        var path = filePath.GetString() ?? "";
                        if (string.IsNullOrEmpty(path))
                        {
                            Log("[ERROR] file_path参数为空");
                            return "file_path参数为空";
                        }
                        Log($"[DEBUG] 读取文件: {path}");
                        var result = ReadFileContent(path);
                        Log("[DEBUG] 读取文件完成");
                        return result;
                    }
                    Log("[ERROR] 缺少file_path参数");
                    return "缺少file_path参数";

                case "write_file":
                    if (args.TryGetValue("file_path", out var writePath) && args.TryGetValue("content", out var writeContent))
                    {
                        var path = writePath.GetString() ?? "";
                        var content = writeContent.GetString() ?? "";
                        if (string.IsNullOrEmpty(path))
                        {
                            Log("[ERROR] file_path参数为空");
                            return "file_path参数为空";
                        }
                        Log($"[DEBUG] 写入文件: {path}");
                        var writeResult = WriteFileContent(path, content);
                        Log("[DEBUG] 写入文件完成");
                        return writeResult;
                    }
                    Log("[ERROR] 缺少必要参数file_path或content");
                    return "缺少必要参数file_path或content";

                case "list_directory":
                    if (args.TryGetValue("directory_path", out var dirPath))
                    {
                        var path = dirPath.GetString() ?? "";
                        if (string.IsNullOrEmpty(path))
                        {
                            Log("[ERROR] directory_path参数为空");
                            return "directory_path参数为空";
                        }
                        Log($"[DEBUG] 列出目录: {path}");
                        var dirResult = ListDirectory(path);
                        Log("[DEBUG] 列出目录完成");
                        return dirResult;
                    }
                    Log("[ERROR] 缺少directory_path参数");
                    return "缺少directory_path参数";

                case "search_web":
                    if (args.TryGetValue("query", out var query))
                    {
                        var searchQuery = query.GetString() ?? "";
                        if (string.IsNullOrEmpty(searchQuery))
                        {
                            Log("[ERROR] query参数为空");
                            return "query参数为空";
                        }
                        Log($"[DEBUG] 搜索网络: {searchQuery}");
                        OnToolCallStart?.Invoke("search_web");
                        var searchResult = await SearchWebAsync(searchQuery);
                        Log("[DEBUG] 搜索完成");
                        return searchResult;
                    }
                    Log("[ERROR] 缺少query参数");
                    return "缺少query参数";

                case "list_staging_files":
                    Log("[DEBUG] 列出暂存区文件");
                    OnToolCallStart?.Invoke("list_staging_files");
                    var stagingFiles = GetStagingFilesList();
                    Log("[DEBUG] 暂存区文件列表完成");
                    return stagingFiles;

                case "get_weather":
                    if (args.TryGetValue("city", out var cityArg))
                    {
                        var city = cityArg.GetString() ?? "";
                        if (string.IsNullOrEmpty(city))
                        {
                            Log("[ERROR] city参数为空");
                            return "city参数为空";
                        }
                        Log($"[DEBUG] 查询天气: {city}");
                        OnToolCallStart?.Invoke("get_weather");
                        var weatherResult = await _weatherService.GetWeatherAsync(city);
                        Log("[DEBUG] 天气查询完成");
                        return weatherResult;
                    }
                    Log("[ERROR] 缺少city参数");
                    return "缺少city参数";

                case "execute_shell":
                    if (args.TryGetValue("command", out var commandArg))
                    {
                        var command = commandArg.GetString() ?? "";
                        var shell = "powershell";
                        if (args.TryGetValue("shell", out var shellArg))
                        {
                            shell = shellArg.GetString() ?? "powershell";
                        }
                        
                        if (string.IsNullOrEmpty(command))
                        {
                            Log("[ERROR] command参数为空");
                            return "command参数为空";
                        }
                        
                        if (!EnableShellExecution)
                        {
                            return "Shell执行功能未启用，请在设置中启用Shell执行功能喵~";
                        }
                        
                        Log($"[DEBUG] 执行Shell命令: {command} (shell: {shell})");
                        OnToolCallStart?.Invoke("execute_shell");
                        
                        var result = await ExecuteShellCommandAsync(command, shell);
                        Log("[DEBUG] Shell命令执行完成");
                        return result;
                    }
                    Log("[ERROR] 缺少command参数");
                    return "缺少command参数";

                case "list_ai_work_folder":
                    Log("[DEBUG] 列出AI工作文件夹");
                    OnToolCallStart?.Invoke("list_ai_work_folder");
                    var workFolderResult = GetAiWorkFolderFiles();
                    Log("[DEBUG] AI工作文件夹列出完成");
                    return workFolderResult;

                default:
                    Log($"[ERROR] 未知工具: {toolName}");
                    return $"未知工具: {toolName}";
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 执行工具失败: {ex.Message}");
            Log($"[ERROR] 异常堆栈: {ex.StackTrace}");
            return $"执行工具失败: {ex.Message}";
        }
    }

    private async Task SendNonStreamingRequestAsync(StringContent content, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            OnError?.Invoke($"API错误 ({response.StatusCode}): {errorContent}");
            return;
        }

        var responseData = await response.Content.ReadAsStringAsync(cancellationToken);
        
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseData);
            var choices = jsonDoc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                
                if (message.TryGetProperty("tool_calls", out var toolCalls))
                {
                    foreach (var toolCall in toolCalls.EnumerateArray())
                    {
                        string toolCallId = "";
                        string toolCallName = "";
                        string toolCallArgs = "";
                        
                        if (toolCall.TryGetProperty("id", out var id))
                            toolCallId = id.GetString() ?? "";
                        
                        // 如果没有 id，生成一个唯一 id
                        if (string.IsNullOrEmpty(toolCallId))
                        {
                            toolCallId = $"call_{Guid.NewGuid():N}";
                            Log($"[DEBUG] 工具调用无id，生成唯一id: {toolCallId}");
                        }
                        
                        if (toolCall.TryGetProperty("function", out var function))
                        {
                            if (function.TryGetProperty("name", out var name))
                                toolCallName = name.GetString() ?? "";
                            if (function.TryGetProperty("arguments", out var args))
                                toolCallArgs = args.GetString() ?? "";
                        }
                        
                        OnToolCallStart?.Invoke(toolCallName);
                        
                        // 先添加 Assistant 的 tool_call 消息
                        _conversationHistory.Add(new Message 
                        {
                            Role = "assistant",
                            Content = null,
                            ToolCalls = new List<ToolCall> 
                            { 
                                new ToolCall 
                                { 
                                    Id = toolCallId,
                                    Type = "function",
                                    Function = new FunctionCall 
                                    { 
                                        Name = toolCallName, 
                                        Arguments = toolCallArgs 
                                    } 
                                } 
                            }
                        });
                        
                        // 执行工具并添加结果
                        var toolResult = await ExecuteToolCallAsync(toolCallName, toolCallArgs);
                        
                        _conversationHistory.Add(new Message 
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCallId
                        });
                    }
                    
                    var continueRequest = new ChatRequest
                    {
                        Model = _settings.Model,
                        Messages = BuildMessages(),
                        Tools = _settings.EnableToolCalls ? GetToolDefinitions() : null,
                        Stream = false
                    };
                    var continueJson = JsonSerializer.Serialize(continueRequest, _jsonOptions);
                    var continueContent = new StringContent(continueJson, Encoding.UTF8, "application/json");
                    await SendNonStreamingRequestAsync(continueContent, cancellationToken);
                    return;
                }
                else if (message.TryGetProperty("content", out var contentElement))
                {
                    var msgContent = contentElement.GetString();
                    
                    if (!string.IsNullOrEmpty(msgContent))
                    {
                        _conversationHistory.Add(new Message { Role = "assistant", Content = msgContent });
                        OnStreamingResponse?.Invoke(msgContent);
                        Log($"[DEBUG] 非流式: 已添加 Assistant 消息到对话历史，内容长度: {msgContent.Length}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"解析响应失败: {ex.Message}");
        }

        Log($"[DEBUG] 非流式: 准备调用 OnComplete");
        OnComplete?.Invoke();
        Log($"[DEBUG] 非流式: OnComplete 已调用");
    }

    public string ReadFileContent(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "文件路径不能为空喵~";
            }

            if (!File.Exists(filePath))
            {
                return $"文件不存在nya~: {filePath}";
            }

            var ext = Path.GetExtension(filePath).ToLower();
            var textExts = new[] { ".txt", ".md", ".json", ".xml", ".cs", ".js", ".html", ".css", ".py", ".java", ".c", ".cpp", ".h", ".ts", ".tsx", ".jsx", ".sql", ".yaml", ".yml", ".ini", ".cfg", ".conf", ".log", ".bat", ".ps1", ".sh" };
            
            if (textExts.Contains(ext) || !IsBinaryFile(filePath))
            {
                // 使用Task.Run和超时机制避免卡住
                var readTask = Task.Run(() => {
                    try
                    {
                        var content = File.ReadAllText(filePath);
                        var fileName = Path.GetFileName(filePath);
                        return $"✅ 文件读取成功: {fileName}\n\n```\n{content}\n```";
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] 读取文件内容失败: {ex.Message}");
                        throw;
                    }
                });

                // 设置10秒超时
                if (!readTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    Log("[ERROR] 读取文件超时");
                    return "❌ 读取文件超时，可能是文件过大或网络共享响应缓慢";
                }

                return readTask.Result;
            }
            else
            {
                return $"❌ 无法读取二进制文件nya~: {filePath}";
            }
        }
        catch (UnauthorizedAccessException)
        {
            return $"❌ 没有权限读取文件喵~: {filePath}";
        }
        catch (Exception ex)
        {
            return $"❌ 读取文件失败: {ex.Message}";
        }
    }

    public string WriteFileContent(string filePath, string content)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "文件路径不能为空喵~";
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
            return $"✅ 文件写入成功喵~: {filePath}";
        }
        catch (UnauthorizedAccessException)
        {
            return $"❌ 没有权限写入文件喵~: {filePath}";
        }
        catch (Exception ex)
        {
            return $"❌ 写入文件失败: {ex.Message}";
        }
    }

    public string ListDirectory(string directoryPath)
    {
        try
        {
            Log($"[DEBUG] 开始列出目录: {directoryPath}");
            
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                Log("[DEBUG] 目录路径为空");
                return "目录路径不能为空喵~";
            }

            // 尝试解析路径，处理可能的转义问题
            directoryPath = directoryPath.Trim('"');
            
            // 处理常见目录名
            directoryPath = ResolveCommonPath(directoryPath);
            
            Log($"[DEBUG] 处理后的路径: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                Log($"[DEBUG] 目录不存在: {directoryPath}");
                return $"目录不存在nya~: {directoryPath}";
            }

            Log($"[DEBUG] 目录存在，开始获取子目录和文件");
            
            // 使用Task.Run和超时机制避免卡住
            var resultTask = Task.Run(() => {
                try
                {
                    // 限制获取的文件数量，避免性能问题
                    var dirs = Directory.GetDirectories(directoryPath).Take(50).ToArray();
                    var files = Directory.GetFiles(directoryPath).Take(100).ToArray();

                    Log($"[DEBUG] 获取到 {dirs.Length} 个目录，{files.Length} 个文件");
                    var result = $"📁 目录: {directoryPath}\n\n";
                    
                    foreach (var dir in dirs)
                    {
                        result += $"📂 {Path.GetFileName(dir)}/\n";
                    }
                    
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        result += $"📄 {Path.GetFileName(file)} ({fileInfo.Length / 1024.0:F1} KB)\n";
                    }

                    if (dirs.Length >= 50 || files.Length >= 100)
                    {
                        result += "\n⚠️ 结果已截断，只显示部分内容喵~";
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] 获取目录内容失败: {ex.Message}");
                    throw;
                }
            });

            // 设置10秒超时
            if (!resultTask.Wait(TimeSpan.FromSeconds(10)))
            {
                Log("[ERROR] 列出目录超时");
                return "❌ 列出目录超时，可能是目录过大或网络共享响应缓慢";
            }

            var result = resultTask.Result;
            Log($"[DEBUG] 目录列出完成，结果长度: {result.Length}");
            return result;
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 列出目录失败: {ex.Message}");
            Log($"[ERROR] 异常堆栈: {ex.StackTrace}");
            return $"❌ 列出目录失败: {ex.Message}";
        }
    }

    public string ReadImageDescription(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                return "图片文件不存在喵~";
            }

            var ext = Path.GetExtension(imagePath).ToLower();
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };

            if (!imageExts.Contains(ext))
            {
                return "不支持的图片格式喵~";
            }

            var fileInfo = new FileInfo(imagePath);
            return $"🖼️ 图片: {Path.GetFileName(imagePath)}\n📏 大小: {fileInfo.Length / 1024.0:F2} KB\n📍 路径: {imagePath}";
        }
        catch (Exception ex)
        {
            return $"读取图片信息失败: {ex.Message}";
        }
    }

    public async Task<string> SearchWebAsync(string query)
    {
        try
        {
            Log($"[DEBUG] 开始搜索并抓取: {query}");

            OnToolCallStart?.Invoke("search_web");

            var result = await _webScraper.SearchAndScrapeAsync(query, maxResults: 3);

            if (result.ScrapedPages.Count == 0)
            {
                return $"未找到关于「{query}」的相关信息喵~";
            }

            var formattedResult = _webScraper.FormatForAI(result);
            Log($"[DEBUG] 搜索抓取完成，找到 {result.ScrapedPages.Count} 个页面");

            return formattedResult;
        }
        catch (HttpRequestException ex)
        {
            Log($"[ERROR] 网络请求失败: {ex.Message}");
            return $"❌ 搜索失败：网络连接错误喵~ {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            Log("[ERROR] 搜索超时");
            return "❌ 搜索超时，请稍后重试喵~";
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 搜索异常: {ex.Message}");
            return $"❌ 搜索失败：{ex.Message}";
        }
    }

    private string GetStagingFilesList()
    {
        try
        {
            if (GetStagingFilesCallback == null)
            {
                return "暂存区功能暂不可用喵~ 请稍后再试nya~";
            }

            var items = GetStagingFilesCallback();
            var fileItems = items.Where(i => i.ItemType == ClipboardItemType.File).ToList();

            if (fileItems.Count == 0)
            {
                return "暂存区目前没有任何文件喵~ 你可以复制一些文件过来nya~ 🐾";
            }

            var result = "📁 暂存区文件列表：\n\n";
            for (int i = 0; i < fileItems.Count; i++)
            {
                var item = fileItems[i];
                var fileName = item.DisplayText;
                var filePath = item.FilePaths?.FirstOrDefault() ?? "未知路径";
                result += $"{i + 1}. 📄 **{fileName}**\n   📍 {filePath}\n\n";
            }

            result += $"共 {fileItems.Count} 个文件喵~ 🐾";
            return result;
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 获取暂存区文件失败: {ex.Message}");
            return $"❌ 获取暂存区文件失败: {ex.Message}";
        }
    }

    private bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[8192];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private bool IsHighRiskCommand(string command)
    {
        var lowerCommand = command.ToLower();
        foreach (var pattern in _highRiskPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(lowerCommand, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<string> ExecuteShellCommandAsync(string command, string shellType)
    {
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        try
        {
            if (IsHighRiskCommand(command))
            {
                if (OnConfirmHighRiskCommand != null)
                {
                    var confirmed = await OnConfirmHighRiskCommand.Invoke(command);
                    if (!confirmed)
                    {
                        return "❌ 用户取消了高风险命令的执行喵~";
                    }
                }
                else
                {
                    return "❌ 高风险命令需要用户确认，但确认回调未设置喵~";
                }
            }

            OnShellOutput?.Invoke($"▶ 正在执行: {command}");
            OnShellOutput?.Invoke($"🔧 使用Shell: {(shellType.ToLower() == "cmd" ? "CMD" : "PowerShell")}");
            OnShellOutput?.Invoke("---");

            var psi = new System.Diagnostics.ProcessStartInfo();
            
            if (shellType.ToLower() == "cmd")
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c {command}";
            }
            else
            {
                psi.FileName = "powershell.exe";
                psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"";
            }

            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
            psi.StandardErrorEncoding = System.Text.Encoding.UTF8;

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                OnShellError?.Invoke("❌ 无法启动进程喵~");
                return "❌ 无法启动进程喵~";
            }

            var outputTask = Task.Run(async () =>
            {
                var buffer = new char[1024];
                while (!process.HasExited)
                {
                    var bytesRead = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var text = new string(buffer, 0, bytesRead);
                        outputBuilder.Append(text);
                        OnShellOutput?.Invoke(text);
                    }
                    await Task.Delay(50);
                }
                var remaining = await process.StandardOutput.ReadToEndAsync();
                if (!string.IsNullOrEmpty(remaining))
                {
                    outputBuilder.Append(remaining);
                    OnShellOutput?.Invoke(remaining);
                }
            });

            var errorTask = Task.Run(async () =>
            {
                var buffer = new char[1024];
                while (!process.HasExited)
                {
                    var bytesRead = await process.StandardError.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var text = new string(buffer, 0, bytesRead);
                        errorBuilder.Append(text);
                        OnShellError?.Invoke(text);
                    }
                    await Task.Delay(50);
                }
                var remaining = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrEmpty(remaining))
                {
                    errorBuilder.Append(remaining);
                    OnShellError?.Invoke(remaining);
                }
            });

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync()), timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill(true);
                OnShellError?.Invoke("❌ 命令执行超时（30秒）喵~");
                OnShellComplete?.Invoke();
                return "❌ 命令执行超时（30秒）喵~";
            }

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            var result = "";
            if (!string.IsNullOrWhiteSpace(output))
            {
                result += $"📤 输出:\n{output}";
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                if (!string.IsNullOrWhiteSpace(result))
                    result += "\n\n";
                result += $"❌ 错误:\n{error}";
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                result = "✅ 命令执行成功，无输出喵~";
            }

            result += $"\n\n📊 退出码: {process.ExitCode}";
            
            OnShellOutput?.Invoke($"---");
            OnShellOutput?.Invoke($"📊 退出码: {process.ExitCode}");
            OnShellComplete?.Invoke();

            return result;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log($"[ERROR] Shell执行失败: {ex.Message}");
            OnShellError?.Invoke($"❌ Shell执行失败: {ex.Message}");
            OnShellComplete?.Invoke();
            return $"❌ Shell执行失败: {ex.Message}";
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 执行命令异常: {ex.Message}");
            OnShellError?.Invoke($"❌ 执行命令异常: {ex.Message}");
            OnShellComplete?.Invoke();
            return $"❌ 执行命令异常: {ex.Message}";
        }
    }

    private string GetAiWorkFolderFiles()
    {
        try
        {
            if (string.IsNullOrEmpty(AiWorkFolder))
            {
                return "AI工作文件夹未设置喵~ 请在设置中配置AI工作文件夹nya~";
            }

            if (!Directory.Exists(AiWorkFolder))
            {
                return $"AI工作文件夹不存在nya~: {AiWorkFolder}";
            }

            var files = Directory.GetFiles(AiWorkFolder);
            var dirs = Directory.GetDirectories(AiWorkFolder);

            if (files.Length == 0 && dirs.Length == 0)
            {
                return "AI工作文件夹是空的喵~ 你可以在这里生成一些文件 nya~";
            }

            var result = $"📁 AI工作文件夹: {AiWorkFolder}\n\n";

            foreach (var dir in dirs)
            {
                result += $"📂 {Path.GetFileName(dir)}/\n";
            }

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                result += $"📄 {Path.GetFileName(file)} ({fileInfo.Length / 1024.0:F1} KB)\n";
            }

            result += $"\n共 {dirs.Length} 个文件夹，{files.Length} 个文件喵~ 🐾";
            return result;
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 获取AI工作文件夹失败: {ex.Message}");
            return $"❌ 获取AI工作文件夹失败: {ex.Message}";
        }
    }

    internal class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
        
        [JsonPropertyName("tool_calls")]
        public List<ToolCall>? ToolCalls { get; set; }
    }

    internal class ToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";
        
        [JsonPropertyName("function")]
        public FunctionCall Function { get; set; } = new();
    }

    internal class FunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "";
    }

    private class ChatRequest
    {
        public string Model { get; set; } = "";
        public List<Message> Messages { get; set; } = new();
        public List<ToolDefinition>? Tools { get; set; }
        public string? ToolChoice { get; set; }
        public bool Stream { get; set; }
    }

    private class ToolDefinition
    {
        public string Type { get; set; } = "";
        public FunctionDefinition Function { get; set; } = new();
    }

    private class FunctionDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public ToolParameters Parameters { get; set; } = new();
    }

    private class ToolParameters
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, PropertySchema> Properties { get; set; } = new();
        public List<string>? Required { get; set; }
    }

    private class PropertySchema
    {
        public string Type { get; set; } = "string";
        public string? Description { get; set; }
    }
}
