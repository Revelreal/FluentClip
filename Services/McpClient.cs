using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FluentClip.Services;

/// <summary>
/// 轻量级 MCP 客户端，用于连接 MiniMax MCP 服务器
/// </summary>
public class McpClient : IDisposable
{
    private Process? _process;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    private string _apiKey = "";
    private readonly object _lock = new();
    private readonly Dictionary<string, TaskCompletionSource<string>> _pendingRequests = new();

    public event Action<string>? OnOutput;
    public event Action<string>? OnError;
    public event Action? OnDisconnected;

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>
    /// 检测系统是否有 uvx
    /// </summary>
    public static bool IsNodeAvailable()
    {
        try
        {
            var userLocalBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
            var uvxPath = Path.Combine(userLocalBin, "uvx.exe");

            if (File.Exists(uvxPath))
            {
                return true;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c where uvx",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 && !string.IsNullOrEmpty(output);
        }
        catch
        {
            return false;
        }
    }

    public bool IsConnected => _isRunning && _process != null && !_process.HasExited;

    /// <summary>
    /// 连接到 MCP 服务器
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return true;

        try
        {
            _cts = new CancellationTokenSource();

            var userLocalBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
            var uvxPath = Path.Combine(userLocalBin, "uvx.exe");

            if (!File.Exists(uvxPath))
            {
                Log($"[MCP] uvx 不存在: {uvxPath}");
                Cleanup();
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = uvxPath,
                Arguments = "minimax-coding-plan-mcp -y",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            startInfo.Environment["MINIMAX_API_KEY"] = _apiKey;
            startInfo.Environment["MINIMAX_API_HOST"] = "https://api.minimaxi.com";
            startInfo.Environment["PATH"] = $"{userLocalBin};{Environment.GetEnvironmentVariable("PATH")}";

            _process = new Process { StartInfo = startInfo };
            _process.EnableRaisingEvents = true;
            _process.Exited += Process_Exited;

            Log("[MCP] 启动 MCP 服务器...");
            _process.Start();

            _isRunning = true;

            // 启动异步读取输出
            _ = Task.Run(() => ReadOutputAsync(), cancellationToken);

            // 等待服务器启动
            Log("[MCP] 等待服务器启动（可能需要下载 Python 运行时）...");
            await Task.Delay(5000, cancellationToken);

            if (_process.HasExited)
            {
                Log($"[MCP] 服务器进程提前退出，退出码: {_process.ExitCode}");
                Cleanup();
                return false;
            }

            // 发送初始化
            var initSuccess = await SendInitializeAsync();
            if (!initSuccess)
            {
                Log("[MCP] 初始化失败");
                Cleanup();
                return false;
            }

            return IsConnected;
        }
        catch (Exception ex)
        {
            Log($"[MCP] 连接异常: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    private async Task ReadOutputAsync()
    {
        try
        {
            while (_isRunning && _process != null && !_process.HasExited)
            {
                try
                {
                    var line = await _process.StandardOutput.ReadLineAsync();
                    if (line == null) break;

                    Log($"[MCP] 收到: {line}");
                    OnOutput?.Invoke(line);

                    // 解析响应并触发等待的请求
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("id", out var idElement))
                        {
                            var id = idElement.ToString();
                            lock (_lock)
                            {
                                if (_pendingRequests.TryGetValue(id, out var tcs))
                                {
                                    _pendingRequests.Remove(id);
                                    tcs.TrySetResult(line);
                                }
                            }
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log($"[MCP] 读取输出异常: {ex.Message}");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[MCP] ReadOutputAsync 异常: {ex.Message}");
        }
    }

    private async Task<bool> SendInitializeAsync()
    {
        if (!IsConnected) return false;

        var initRequest = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize",
            ["params"] = new Dictionary<string, object>
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new Dictionary<string, object>(),
                ["clientInfo"] = new Dictionary<string, string>
                {
                    ["name"] = "fluentclip",
                    ["version"] = "1.0"
                }
            }
        };

        Log("[MCP] 发送 initialize 请求...");
        var json = JsonSerializer.Serialize(initRequest);

        try
        {
            await _process!.StandardInput.WriteLineAsync(json);
            await _process.StandardInput.FlushAsync();
            Log($"[MCP] 已发送: {json}");

            // 等待响应（使用 TaskCompletionSource）
            var tcs = new TaskCompletionSource<string>();
            lock (_lock)
            {
                _pendingRequests["1"] = tcs;
            }

            // 等待最多 30 秒
            var timeoutTask = Task.Delay(30000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == tcs.Task && tcs.Task.Result != null)
            {
                Log("[MCP] 收到 initialize 响应");
            }
            else
            {
                Log("[MCP] 等待 initialize 响应超时");
            }

            // 发送 initialized 通知
            var initializedNotification = new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "initialized",
                ["params"] = new Dictionary<string, object>()
            };

            var notifJson = JsonSerializer.Serialize(initializedNotification);
            await _process.StandardInput.WriteLineAsync(notifJson);
            await _process.StandardInput.FlushAsync();
            Log("[MCP] 已发送 initialized 通知");

            return true;
        }
        catch (Exception ex)
        {
            Log($"[MCP] 发送初始化请求失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 调用 MCP 工具
    /// </summary>
    public async Task<string?> CallToolAsync(string toolName, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected) return null;
        }

        var requestId = Guid.NewGuid().ToString("N")[..8];
        var request = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "tools/call",
            ["params"] = new Dictionary<string, object>
            {
                ["name"] = toolName,
                ["arguments"] = args ?? new Dictionary<string, object>()
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(request);
            await _process!.StandardInput.WriteLineAsync(json);
            await _process.StandardInput.FlushAsync();
            Log($"[MCP] 发送工具调用: {json}");

            // 创建等待响应的 TaskCompletionSource
            var tcs = new TaskCompletionSource<string>();
            lock (_lock)
            {
                _pendingRequests[requestId] = tcs;
            }

            // 等待响应（图片理解和搜索需要更长时间，最多 120 秒）
            var timeoutTask = Task.Delay(120000, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == tcs.Task)
            {
                try
                {
                    var response = await tcs.Task;
                    Log($"[MCP] 收到工具调用响应");
                    return response;
                }
                catch (Exception ex)
                {
                    Log($"[MCP] 任务异常: {ex.Message}");
                    return null;
                }
            }
            else
            {
                Log("[MCP] 等待工具调用响应超时（120秒）");
                lock (_lock)
                {
                    _pendingRequests.Remove(requestId);
                }
                // 返回 null 表示调用失败，调用者会使用回退机制
                return null;
            }
        }
        catch (Exception ex)
        {
            Log($"[MCP] 工具调用失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 列出所有可用工具
    /// </summary>
    public async Task<List<McpTool>?> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            var connected = await ConnectAsync(cancellationToken);
            if (!connected) return null;
        }

        return new List<McpTool>
        {
            new McpTool { Name = "web_search", Description = "网络搜索工具" },
            new McpTool { Name = "understand_image", Description = "图片理解工具" }
        };
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        Log("[MCP] 服务器进程已退出");
        _isRunning = false;
        OnDisconnected?.Invoke();
    }

    public void Disconnect()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        _isRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        lock (_lock)
        {
            foreach (var tcs in _pendingRequests.Values)
            {
                tcs.TrySetCanceled();
            }
            _pendingRequests.Clear();
        }

        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        catch { }

        _process?.Dispose();
        _process = null;
    }

    private void Log(string message)
    {
        var logDir = Path.Combine(StorageService.GetAppDataPath(), "logs");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, $"mcp_{DateTime.Now:yyyyMMdd}.log");
        try
        {
            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose()
    {
        Disconnect();
    }
}

public class McpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
