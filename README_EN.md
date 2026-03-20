# <img src="https://img.shields.io/badge/FluentClip-📎-blue?style=flat&logo=clippy" height="28"> FluentClip

<p align="center">
  <b>Smart Clipboard Manager</b> · <b>AI Assistant</b> · Windows Desktop App
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

## ⭐ Features

### 📋 Clipboard Management

| | |
|:---|:---|
| 🔄 **History** | Auto-save text, images, files with search |
| 🔍 **Quick Search** | Real-time filtering, instant results |
| ⭐ **Favorites** | Star items for quick access |
| 🗑️ **Trash Zone** | Drag to delete, protect privacy |

### 🤖 AI Assistant

| | |
|:---|:---|
| 💬 **Smart Chat** | Powered by MiniMax API |
| 📝 **Markdown** | Code highlighting, table rendering |
| 🔧 **Tool Calling** | File I/O, directory listing, web search |
| 🎭 **Custom Persona** | System Prompt for unique AI personality |

### 🖥️ System Integration

| | |
|:---|:---|
| 📍 **System Tray** | Run in background |
| ⌨️ **Global Hotkeys** | Quick access anytime |
| 🎨 **Fluent Design** | Windows 11 design language |
| 💾 **Auto Save** | Persistent settings |

---

## 🚀 Quick Start

```bash
# Clone
git clone https://github.com/fluentclip/fluentclip.git
cd fluentclip

# Build
dotnet build

# Run
dotnet run
```

> 💡 **Tip**: Publish as single file `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

---

## ⌨️ Shortcuts

| Action | Shortcut |
|:---|:---|
| Show Window | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>V</kbd> |
| Open Settings | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> |
| Toggle On Top | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> |

---

## 🏗️ Architecture

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

## 🛠️ Tech Stack

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│    .NET 9    │  │     WPF      │  │  Community   │
│   Runtime    │  │     UI       │  │   MVVM       │
└──────────────┘  └──────────────┘  └──────────────┘
       │                 │                 │
       ▼                 ▼                 ▼
  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
  │  MiniMax API │  │   Windows    │  │   GitHub    │
  │   AI Power   │  │Integration   │  │  Collaboration
  └──────────────┘  └──────────────┘  └──────────────┘
```

---

## ⚙️ Configuration

### AI Assistant Setup

```
Settings → AI Assistant
  ├── Base URL: https://api.minimaxi.com/v1
  ├── API Key:  [Get from platform.minimaxi.com]
  └── Model:    abab6.5s-chat (default)
```

### Data Location

```
%APPDATA%\FluentClip\
  ├── settings.json        # App Settings
  ├── agent_settings.json # AI Settings
  └── logs\               # Runtime Logs
```

---

## 🤝 Contributing

Welcome PRs! Read the [Contributing Guide](CONTRIBUTING.md) for details.

<a href="https://github.com/fluentclip/fluentclip/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=fluentclip/fluentclip&max=12" width="100%">
</a>

---

## 📖 Documentation

| Document | Description |
|:---|:---|
| [中文版](README.md) | 项目概览 |
| [Contributing](CONTRIBUTING.md) | Dev Guide |
| [Plugin Dev](PLUGIN_DEVELOPMENT.md) | Extension Dev |

---

## 📄 License

MIT License · © 2024 FluentClip Team

---

<p align="center">
  <sub>Made with ❤️</sub>
</p>
