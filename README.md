# <img src="https://img.shields.io/badge/FluentClip-📎-blue?style=flat&logo=clippy" height="28"> FluentClip

<p align="center">
  <b>智能剪贴板管理器</b> · <b>AI 助手</b> · Windows 桌面应用
</p>

<p align="center">
  <a href="https://github.com/fluentclip/fluentclip/stargazers">
    <img src="https://img.shields.io/github/stars/fluentclip/fluentclip?style=flat&color=blue" alt="stars">
  </a>
  <a href="https://github.com/fluentclip/fluentclip/network/members">
    <img src="https://img.shields.io/github/forks/fluentclip/fluentclip?style=flat&color=blue" alt="forks">
  </a>
  <img src="https://img.shields.io/badge/platform-Windows%2010/11-blue?style=flat" alt="platform">
  <img src="https://img.shields.io/badge/.NET-9.0-blue?style=flat&logo=.net" alt=".NET">
  <img src="https://img.shields.io/badge/license-MIT-blue?style=flat" alt="license">
</p>

---

## ⭐ 功能亮点

### 📋 剪贴板管理

| | |
|:---|:---|
| � **历史记录** | 自动保存文本、图片、文件，随时回溯 |
| 🔍 **智能搜索** | 实时过滤，输入即所得 |
| ⭐ **收藏整理** | 标记常用项，一键直达 |
| 🗑️ **安全删除** | 拖拽到垃圾桶，隐私保护 |

### 🤖 AI 助手

| | |
|:---|:---|
| 💬 **智能对话** | MiniMax API 驱动，会思考的助手 |
| 📝 **Markdown** | 代码高亮、表格渲染，阅读舒适 |
| 🛠️ **工具调用** | 文件读写、目录列表、联网搜索、执行Shell命令 |
| 🎭 **人格定制** | System Prompt，塑造专属 AI 个性 |
| 📊 **上下文管理** | Token使用显示，自动总结优化 |
| 📁 **AI工作文件夹** | 指定文件夹，AI生成文件自动进入暂存区 |
| 🐱 **猫羽雫** | 可爱猫猫形象，对话后随机小Tips |
| 🌐 **热点分享** | 定时搜索热门资讯，主动分享给你 |

### 🐱 猫羽雫悬浮窗

| | |
|:---|:---|
| ✨ **物理效果** | 拖拽飞行、弹性吸附，Q萌可爱 |
| 💬 **智能气泡** | 文件复制时主动聊天，提及文件名/大小 |
| 🎲 **题外话** | 连续操作后随机闲聊，像朋友一样陪你 |
| 🌟 **热点新闻** | 飞行/吸附状态定时搜索热点，推送给你 |

### 🖥️ 系统集成

| | |
|:---|:---|
| 📍 **系统托盘** | 最小化后台，不打扰工作 |
| ⌨️ **全局热键** | 快捷键随时调出 |
| 🎨 **Fluent Design** | Windows 11 设计语言 |
| 💾 **自动保存** | 设置持久化，无后顾之忧 |

---

## 🚀 快速开始

```bash
# 克隆
git clone https://github.com/fluentclip/fluentclip.git
cd fluentclip

# 构建
dotnet build

# 运行
dotnet run
```

> 💡 **提示**: 发布为单文件 `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

---

## ⌨️ 快捷键

| 功能 | 快捷键 |
|:---|:---|
| 显示主窗口 | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>V</kbd> |
| 打开设置 | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> |
| 窗口置顶 | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> |

---

## 🏗️ 架构一览

```
┌────────────────────────────────────────────────────┐
│                     FluentClip                      │
├────────────────────────────────────────────────────┤
│  Core/           Events/        Factories/        │
│  ├─ API          ├─EventAggre   ├─ClipboardItem   │
│  └─PluginMgr     └─ClipboardE    └─ Factory        │
├────────────────────────────────────────────────────┤
│  Services/                          Models/        │
│  ├─ClipboardService  ├─Hotkey    ├─ClipboardItem  │
│  ├─AgentService      ├─Toast     ├─AppSettings     │
│  └─ThumbnailHelper   └─DragDrop  └─AgentSettings   │
└────────────────────────────────────────────────────┘
```

---

## 🛠️ 技术栈

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│    .NET 9    │  │     WPF      │  │  Community   │
│   Runtime    │  │     UI       │  │   MVVM       │
└──────────────┘  └──────────────┘  └──────────────┘
       │                 │                 │
       ▼                 ▼                 ▼
  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
  │  MiniMax API │  │   Windows     │  │  GitHub      │
  │   AI 能力    │  │   系统集成    │  │   协作       │
  └──────────────┘  └──────────────┘  └──────────────┘
```

---

## ⚙️ 配置

### AI 助手设置

```
设置 → AI 助手
  ├── Base URL: https://api.minimaxi.com/v1
  ├── API Key:  [从 platform.minimaxi.com 获取]
  └── 模型:     abab6.5s-chat (默认)
```

### 数据位置

```
%APPDATA%\FluentClip\
  ├── settings.json        # 应用设置
  ├── agent_settings.json # AI 设置
  └── logs\               # 运行日志
```

---

## 🤝 参与贡献

欢迎提交 Pull Request！请阅读 [贡献指南](CONTRIBUTING.md) 了解详情。

<a href="https://github.com/fluentclip/fluentclip/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=fluentclip/fluentclip&max=12" width="100%">
</a>

---

## � 文档

| 文档 | 说明 |
|:---|:---|
| [README](README.md) | 项目概览 |
| [贡献指南](CONTRIBUTING.md) | 开发指引 |
| [插件开发](PLUGIN_DEVELOPMENT.md) | 扩展开发 |
| [English](README_EN.md) | English Version |

---

## 📄 许可证

MIT License · © 2024 FluentClip Team

---

<p align="center">
  <sub>Made with ❤️</sub>
</p>
