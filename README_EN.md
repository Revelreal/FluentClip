# FluentClip

An elegant Windows clipboard manager with built-in AI assistant functionality.

## Features

### Clipboard Management
- 📋 **Clipboard History** - Automatically records clipboard history, supporting text, images, and files
- ⌨️ **Global Hotkeys** - Customizable keyboard shortcuts for quick access
- 📌 **Always on Top** - Pin window to stay visible
- 🎨 **macOS-style UI** - Modern and clean interface design

### AI Assistant
- 🤖 **MiniMax API Integration** - Powered by MiniMax large language model
- 💬 **Streaming Response** - Real-time streaming output for better experience
- 🧠 **Thinking Tag Rendering** - Supports collapsible `<think>` / `</think>` thinking tags
- 🔌 **Tool Calling (Experimental)** - Supports Function Calling for file reading, writing, directory listing, and web search
- 🎭 **Customizable Persona** - System Prompt customization for AI personality

### System Integration
- 🪟 **System Tray** - Run in background with system tray support
- ⚡ **Global Hotkeys** - Quick access via keyboard shortcuts
- 💾 **Persistent Settings** - Auto-save user configurations

## Keyboard Shortcuts

| Shortcut | Function |
|----------|----------|
| `Ctrl+Shift+V` | Show main window |
| `Ctrl+Shift+S` | Open settings window |
| `Ctrl+Shift+P` | Toggle always on top |

## Tech Stack

- **.NET 9.0** - Modern .NET runtime
- **WPF** - Windows Presentation Foundation UI framework
- **MVVM Architecture** - Using CommunityToolkit.Mvvm
- **MiniMax API** - AI capability support

## Project Structure

```
FluentClip/
├── Models/                 # Data models
│   ├── AgentSettings.cs    # AI assistant settings
│   ├── AppSettings.cs      # Application settings
│   └── ClipboardItem.cs    # Clipboard item
├── Services/               # Business logic
│   ├── AgentService.cs     # AI assistant service
│   ├── ClipboardService.cs # Clipboard monitoring
│   ├── HotkeyManager.cs    # Global hotkey management
│   └── MarkdownRenderer.cs # Markdown rendering
├── ViewModels/             # MVVM ViewModels
│   └── MainViewModel.cs    # Main view model
├── MainWindow.xaml        # Main window
├── SettingsWindow.xaml    # Settings window
├── AgentSettingsWindow.xaml # AI settings window
└── App.xaml               # Application entry
```

## Build & Run

### Development Mode

```bash
cd FluentClip
dotnet build
dotnet run
```

### Release Build

```bash
# Publish as self-contained executable (no .NET installation required)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Configuration

### AI Assistant Settings

Configure MiniMax API before first use:

1. Open AI Assistant settings
2. Fill in Base URL (default: `https://api.minimaxi.com/v1`)
3. Fill in API Key
4. Select model (default: `MiniMax-M2.5`)
5. Optional: Customize System Prompt for AI personality
6. Optional: Enable "Tool Calling" experimental feature

### Settings Storage Location

- App settings: `%APPDATA%\FluentClip\settings.json`
- AI settings: `%APPDATA%\FluentClip\agent_settings.json`
- Logs: `logs/agent_YYYYMMDD_HHmmss.log`

## Notes

- Tool calling is experimental and disabled by default
- AI assistant requires a valid MiniMax API Key
- Some features may require administrator privileges (e.g., global hotkeys)

## License

MIT License
