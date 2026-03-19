# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FluentClip is a Windows desktop clipboard manager application built with WPF and .NET 9.0. It monitors clipboard changes, maintains a history of clipboard items (text, images, files), and includes an AI assistant feature powered by MiniMax API.

## Build Commands

```bash
# Build the project
dotnet build

# Run in debug mode
dotnet run

# Publish as self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true
```

## Architecture

The application follows the MVVM pattern using CommunityToolkit.Mvvm:

- **Models/** - Data models (ClipboardItem, AppSettings, AgentSettings)
- **ViewModels/** - MVVM ViewModels (MainViewModel)
- **Services/** - Business logic services (ClipboardService, HotkeyManager, AgentService, MarkdownRenderer)
- **XAML Files** - Windows and UI components (MainWindow, SettingsWindow, AgentWindow, AgentSettingsWindow)

### Key Components

- **ClipboardService**: Monitors system clipboard using Windows API (AddClipboardFormatListener)
- **HotkeyManager**: Registers global hotkeys using Windows API (RegisterHotKey)
- **AgentService**: Integrates with MiniMax API for AI assistant functionality with function calling support
- **MainViewModel**: Manages clipboard history and copy/paste operations

### Global Hotkeys

- `Ctrl+Shift+V` - Show main window
- `Ctrl+Shift+S` - Open settings window
- `Ctrl+Shift+P` - Pin/unpin window

### Settings Storage

Settings are stored in JSON format at:
- `%APPDATA%\FluentClip\settings.json` - App settings
- `%APPDATA%\FluentClip\agent_settings.json` - AI agent settings

## Development Notes

- The project uses nullable reference types disabled (`<Nullable>disable</Nullable>`)
- Both WPF and WindowsForms are enabled for compatibility
- Agent logs are written to `logs/agent_YYYYMMDD_HHmmss.log` in the application directory
