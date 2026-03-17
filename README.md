# FluentClip

一个优雅的 Windows 剪贴板管理器，内置 AI 助手功能。

## 功能特点

### 剪贴板管理
- 📋 **剪贴板历史记录** - 自动记录剪贴板历史，支持文本、图片、文件
- ⌨️ **全局快捷键** - 支持自定义快捷键快速调用
- 📌 **窗口置顶** - 可以将窗口置顶显示
- 🎨 **macOS 风格 UI** - 现代简洁的界面设计

### AI 助手
- 🤖 **MiniMax API 集成** - 基于 MiniMax 大语言模型
- 💬 **流式回复** - 支持实时流式输出，体验更流畅
- 🧠 **思考标签渲染** - 支持 `<think>` / `</think>` 思考标签的折叠显示
- 🔌 **工具调用（实验性）** - 支持 Function Calling，可读取文件、写入文件、列目录、联网搜索
- 🎭 **可自定义人格** - 支持设置 System Prompt，定制 AI 助手性格

### 系统集成
- 🪟 **最小化到托盘** - 支持系统托盘运行
- ⚡ **全局热键** - 快捷键快速调用
- 💾 **设置持久化** - 自动保存用户配置

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Shift+V` | 显示主窗口 |
| `Ctrl+Shift+S` | 打开设置窗口 |
| `Ctrl+Shift+P` | 窗口置顶/取消置顶 |

## 技术栈

- **.NET 9.0** - 现代 .NET 运行时
- **WPF** - Windows Presentation Foundation UI 框架
- **MVVM 架构** - 使用 CommunityToolkit.Mvvm
- **MiniMax API** - AI 能力支持

## 项目结构

```
FluentClip/
├── Models/                 # 数据模型
│   ├── AgentSettings.cs    # AI 助手配置
│   ├── AppSettings.cs      # 应用程序配置
│   └── ClipboardItem.cs    # 剪贴板项
├── Services/               # 业务逻辑
│   ├── AgentService.cs     # AI 助手服务
│   ├── ClipboardService.cs # 剪贴板监控服务
│   ├── HotkeyManager.cs    # 全局热键管理
│   └── MarkdownRenderer.cs  # Markdown 渲染
├── ViewModels/             # MVVM 视图模型
│   └── MainViewModel.cs    # 主视图模型
├── MainWindow.xaml        # 主窗口
├── SettingsWindow.xaml    # 设置窗口
├── AgentSettingsWindow.xaml # AI 助手设置窗口
└── App.xaml               # 应用程序入口
```

## 构建与运行

### 开发模式

```bash
cd FluentClip
dotnet build
dotnet run
```

### 发布版本

```bash
# 发布为自包含可执行文件（无需安装 .NET）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 配置说明

### AI 助手设置

首次使用需要配置 MiniMax API：

1. 打开 AI 助手设置界面
2. 填写 Base URL（默认：`https://api.minimaxi.com/v1`）
3. 填写 API Key
4. 选择模型（默认：`MiniMax-M2.5`）
5. 可选：自定义 System Prompt 调整 AI 人格
6. 可选：开启"工具调用能力"实验性功能

### 设置存储位置

- 应用程序设置：`%APPDATA%\FluentClip\settings.json`
- AI 助手设置：`%APPDATA%\FluentClip\agent_settings.json`
- 运行日志：`logs/agent_YYYYMMDD_HHmmss.log`

## 注意事项

- 工具调用功能目前为实验性功能，默认关闭
- AI 助手需要有效的 MiniMax API Key 才能使用
- 部分功能需要管理员权限（如全局热键）

## 许可证

MIT License
