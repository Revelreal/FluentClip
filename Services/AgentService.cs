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

    public event Action<string>? OnStreamingResponse;
    public event Action? OnComplete;
    public event Action<string>? OnError;
    public event Action<string>? OnToolCallStart;

    public AgentService(AgentSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
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
        var tools = GetToolDefinitions();

        var requestBody = new ChatRequest
        {
            Model = _settings.Model,
            Messages = messages,
            Tools = tools,
            ToolChoice = "auto",
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
        
        if (!string.IsNullOrEmpty(_settings.SystemPrompt))
        {
            messages.Add(new Message { Role = "system", Content = _settings.SystemPrompt });
        }
        
        messages.AddRange(_conversationHistory);
        
        return messages;
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
        var waitingForToolCall = false;
        var toolCallId = "";
        var toolCallName = "";
        var toolCallArgs = "";
        var currentToolCallIndex = -1;

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
                        if (waitingForToolCall)
                        {
                            Log($"[DEBUG] 处理工具调用: {toolCallName}");
                            var toolResult = ExecuteToolCall(toolCallName, toolCallArgs);
                            Log($"[DEBUG] 工具调用完成，结果: {toolResult.Substring(0, Math.Min(100, toolResult.Length))}...");

                            // 添加 Assistant 的 tool_call 消息
                            var toolCallMessage = new Message
                            {
                                Role = "assistant",
                                Content = null,
                                ToolCalls = new List<ToolCall>
                                {
                                    new ToolCall
                                    {
                                        Id = string.IsNullOrEmpty(toolCallId) ? $"call_{currentToolCallIndex}" : toolCallId,
                                        Function = new FunctionCall
                                        {
                                            Name = toolCallName,
                                            Arguments = toolCallArgs
                                        }
                                    }
                                }
                            };
                            _conversationHistory.Add(toolCallMessage);

                            // 添加工具结果消息
                            _conversationHistory.Add(new Message
                            {
                                Role = "tool",
                                Content = toolResult,
                                ToolCallId = string.IsNullOrEmpty(toolCallId) ? (currentToolCallIndex >= 0 ? $"call_{currentToolCallIndex}" : "") : toolCallId
                            });
                            
                            waitingForToolCall = false;
                            toolCallId = "";
                            toolCallName = "";
                            toolCallArgs = "";
                            currentToolCallIndex = -1;
                            
                            Log("[DEBUG] 准备继续请求");
                            var continueRequest = new ChatRequest
                            {
                                Model = _settings.Model,
                                Messages = BuildMessages(),
                                Tools = _settings.EnableToolCalls ? GetToolDefinitions() : null,
                                ToolChoice = _settings.EnableToolCalls ? "auto" : null,
                                Stream = true
                            };
                            var continueJson = JsonSerializer.Serialize(continueRequest, _jsonOptions);
                            var continueContent = new StringContent(continueJson, Encoding.UTF8, "application/json");
                            Log("[DEBUG] 发送继续请求");
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
                        var delta = choices[0].GetProperty("delta");
                        
                        if (delta.TryGetProperty("tool_calls", out var toolCalls))
                        {
                            Log("[DEBUG] 收到 tool_calls");
                            waitingForToolCall = true;
                            foreach (var toolCall in toolCalls.EnumerateArray())
                            {
                                if (toolCall.TryGetProperty("id", out var id))
                                    toolCallId = id.GetString() ?? "";
                                if (toolCall.TryGetProperty("function", out var function))
                                {
                                    if (function.TryGetProperty("name", out var name))
                                        toolCallName += name.GetString() ?? "";
                                    if (function.TryGetProperty("arguments", out var args))
                                        toolCallArgs += args.GetString() ?? "";
                                }
                            }
                            if (!string.IsNullOrEmpty(toolCallName))
                            {
                                OnToolCallStart?.Invoke(toolCallName);
                                currentToolCallIndex++;
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
                catch
                {
                }
            }
        }

        if (fullResponse.Length > 0)
        {
            Log($"[DEBUG] 流式响应完成，内容长度: {fullResponse.Length}");
            _conversationHistory.Add(new Message { Role = "assistant", Content = fullResponse.ToString() });
        }

        OnComplete?.Invoke();
    }

    private string ExecuteToolCall(string toolName, string arguments)
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

            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
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
                        return $"联网搜索功能需要配置支持搜索的API喵~ 当前API不支持搜索nya~";
                    }
                    Log("[ERROR] 缺少query参数");
                    return "缺少query参数";

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
                        if (toolCall.TryGetProperty("function", out var function))
                        {
                            if (function.TryGetProperty("name", out var name))
                                toolCallName = name.GetString() ?? "";
                            if (function.TryGetProperty("arguments", out var args))
                                toolCallArgs = args.GetString() ?? "";
                        }
                        
                        OnToolCallStart?.Invoke(toolCallName);
                        var toolResult = ExecuteToolCall(toolCallName, toolCallArgs);
                        
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
                        ToolChoice = _settings.EnableToolCalls ? "auto" : null,
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
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"解析响应失败: {ex.Message}");
        }

        OnComplete?.Invoke();
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

    private class Message
    {
        public string Role { get; set; } = "";
        public string? Content { get; set; }
        public string? ToolCallId { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
    }

    private class ToolCall
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "function";
        public FunctionCall Function { get; set; } = new();
    }

    private class FunctionCall
    {
        public string Name { get; set; } = "";
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
