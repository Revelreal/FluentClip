# FluentClip 项目状态

## 当前时间
2026-03-27

## 已完成的功能

### MCP 设置界面（用户友好）

**修改文件**：`AgentSettingsWindow.xaml`, `AgentSettingsWindow.xaml.cs`, `Models/AgentSettings.cs`

#### 用户体验设计

```
┌──────────────────────────────────────────────────────────────┐
│  AI Agent 设置                                               │
│                                                              │
│  ┌────────────────────────────────────────────────────┐   │
│  │ 启用 MCP（图片理解 & 联网搜索）          [开关]      │   │
│  │ 需要 Node.js 环境支持                             │   │
│  │                                                    │   │
│  │  ┌─────────────────────────────────────────────┐  │   │
│  │  │ ● Node.js 和 npx 已就绪                      │  │   │
│  │  │   Node: v20.x.x | npx: 10.x.x               │  │   │
│  │  └─────────────────────────────────────────────┘  │   │
│  │                                                    │   │
│  │  [检测环境]  [已就绪]                             │   │
│  └────────────────────────────────────────────────────┘   │
│                                                              │
│  或（未安装时）                                              │
│  ┌────────────────────────────────────────────────────┐   │
│  │ 启用 MCP（图片理解 & 联网搜索）          [开关]      │   │
│  │ ⚠️ 启用 MCP 需要 Node.js 环境                    │   │
│  │                                                    │   │
│  │  ┌─────────────────────────────────────────────┐  │   │
│  │  │ ✗ Node.js 未安装                            │  │   │
│  │  └─────────────────────────────────────────────┘  │   │
│  │                                                    │   │
│  │  [检测环境]  [自动安装]                           │   │
│  └────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

#### 功能特性

| 功能 | 说明 |
|-----|------|
| **MCP 开关** | 用户可选择是否启用 MCP |
| **环境检测** | 实时显示 Node.js 和 npx 安装状态 |
| **状态指示** | 绿色=就绪 / 橙色=npx缺失 / 红色=未安装 |
| **一键安装** | 使用 winget 自动安装 Node.js LTS |
| **手动安装** | 安装失败时提供 nodejs.org 下载链接 |

#### AgentSettings 新增字段

```csharp
public bool EnableMcp { get; set; } = false;
```

#### 自动安装命令

```bash
winget install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements
```

### MCP 客户端（轻量化）

**新增文件**：`Services/McpClient.cs`

#### 设计特点

| 特性 | 实现方式 |
|-----|---------|
| **按需连接** | 只在 EnableMcp=true 时启动进程 |
| **stdio 通信** | JSON-RPC via Process StandardInput/Output |
| **零依赖** | 使用内置 System.Text.Json |
| **自动回退** | MCP 不可用时使用本地实现 |

### AgentService 集成

- `_settings.EnableMcp` 控制是否启用 MCP
- `RefreshMcpStatus()` 方法用于刷新 MCP 状态

## 功能对比

| 功能 | MCP 关闭 | MCP 开启 |
|-----|---------|---------|
| 网络搜索 | WebScraper | WebScraper（fallback） |
| 图片理解 | MiniMax API 多模态 | MiniMax API 多模态（fallback） |
| PDF 读取 | PdfPig | PdfPig |
| AI 对话 | ✓ | ✓ |

## 图片理解实现

当 MCP 的 `understand_image` 不可用时（官方服务器无此工具），回退到 `UnderstandImageDirectlyAsync`：

```csharp
// 直接调用 MiniMax API，多模态格式
var messages = new List<object>
{
    new Message
    {
        Role = "user",
        Content = new List<object>
        {
            new TextContent { Text = prompt },
            new ImageUrlContent { ImageUrl = new ImageUrl { Url = imageBase64 } }
        }
    }
};
```

这是 MiniMax API 原生支持的多模态输入格式，效果比 `[IMAGE]base64[/IMAGE]` 好得多。

## MCP 服务器工具列表

**重要发现：MiniMax 有两个不同的 MCP 服务器：**

### 1. minimax-coding-plan-mcp（当前使用的）

我们实际测试发现，这个服务器只有 **2 个工具**：

| 工具名 | 说明 | 状态 |
|-------|------|------|
| understand_image | 图片理解 | ✅ 测试通过 |
| web_search | 网络搜索 | ✅ 测试通过 |

### 2. MiniMax-MCP（官方完整版）

根据官方文档，这个服务器提供更多工具：

| 工具名 | 说明 |
|-------|------|
| text_to_audio | 文本转语音 |
| list_voices | 列出可用语音 |
| voice_clone | 语音克隆 |
| play_audio | 播放音频 |
| generate_video | 生成视频 |
| query_video_generation | 查询视频生成状态 |
| text_to_image | 生成图片 |
| music_generation | 生成音乐 |
| voice_design | 语音设计 |

**信息来源**：[MiniMax MCP 使用指南](https://platform.minimaxi.com/docs/guides/mcp-guide)

## TTS API 直接调用

Token Plan 的 TTS 功能需要直接调用 API（不是 MCP）：

- **端点**：`/v1/t2a_v2`（不是 `/v1/text_to_speech`）
- **Header**：需要额外添加 `MM-API-Source: Minimax-MCP`
- **模型**：Token Plan 只支持 `speech-2.8-hd`
- **音频格式**：返回 hex 编码，需要 `Buffer.from(hex, 'hex')` 解码

## Token Plan 多模态能力（已验证）

| 功能 | 模型 | 端点 | 状态 |
|-----|------|------|------|
| TTS | `speech-2.8-hd` | `/v1/t2a_v2` | ✅ |
| 图片生成 | `image-01` | `/v1/image_generation` | ✅ |
| 音乐生成 | `music-2.5` | `/v1/music_generation` | ✅ |
| 视频生成 | `MiniMax-Hailuo-2.3` | `/v1/video_generation` | ✅ |

### 视频生成流程（3步）

```csharp
// 1. 创建任务
POST /v1/video_generation
Body: { "model": "MiniMax-Hailuo-2.3", "prompt": "...", "duration": 6, "resolution": "768P" }
返回: { "task_id": "xxx" }

// 2. 轮询状态
GET /v1/query/video_generation?task_id=xxx
状态: Preparing → Queueing → Processing → Success

// 3. 获取下载链接
GET /v1/files/retrieve?file_id=xxx
返回: { "file": { "download_url": "https://..." } }
```

### 音乐生成格式

```csharp
// lyrics 支持结构标签
{
    "model": "music-2.5",
    "prompt": "Happy pop music",
    "lyrics": "[Verse]\nHello world\n\n[Chorus]\nLa la la song"
}
```

### 通用请求 Header

```http
Authorization: Bearer {api_key}
MM-API-Source: Minimax-MCP
Content-Type: application/json
```

## 技术栈

- .NET 9 / WPF
- System.Text.Json（内置）
- Process + stdio
- winget（自动安装）

## 分支信息

- 当前分支：`fix/tool-call-end-conversation`
- 主分支：`main`
