# 🔌 插件开发指南

<p align="center">
  <img src="https://img.shields.io/badge/Plugin%20Development-Customize%20FluentClip-blueviolet?style=for-the-badge">
</p>

> 🧩 本指南将帮助你创建 FluentClip 插件，扩展应用功能

---

## 📋 目录

1. [概述](#-概述)
2. [核心概念](#-核心概念)
3. [插件接口](#-插件接口)
4. [开发示例](#-开发示例)
5. [高级功能](#-高级功能)
6. [调试与发布](#-调试与发布)

---

## 🎯 概述

FluentClip 采用**插件化架构**，允许开发者在不修改核心代码的情况下扩展功能。

### 插件能力

| 能力 | 描述 |
|------|------|
| 🖼️ **自定义缩略图生成器** | 为特定文件类型生成缩略图 |
| 📋 **剪贴板处理器** | 处理特定类型的剪贴内容 |
| 🖱️ **拖拽处理** | 自定义拖拽行为 |
| 🔔 **自定义事件** | 监听和响应应用事件 |
| 🍽️ **菜单扩展** | 添加自定义菜单项 |

---

## 🏗️ 核心概念

### 架构图

```
┌─────────────────────────────────────────────────────────┐
│                    FluentClip App                        │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │   插件 A    │  │   插件 B    │  │   插件 C    │   │
│  │ (缩略图)    │  │ (处理器)    │  │ (拖拽)      │   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘   │
│         │                │                │            │
│         └────────────────┼────────────────┘            │
│                          ▼                              │
│               ┌─────────────────────┐                   │
│               │   PluginManager    │                   │
│               │  (插件生命周期管理)  │                   │
│               └──────────┬──────────┘                   │
│                          │                              │
│               ┌──────────▼──────────┐                   │
│               │   EventAggregator   │                   │
│               │    (事件聚合器)      │                   │
│               └─────────────────────┘                   │
└─────────────────────────────────────────────────────────┘
```

### 关键组件

| 组件 | 命名空间 | 职责 |
|------|----------|------|
| `IPlugin` | `FluentClip.Core.API` | 插件基础接口 |
| `IPluginContext` | `FluentClip.Core.API` | 插件上下文 |
| `PluginManager` | `FluentClip.Core` | 插件加载与管理 |
| `EventAggregator` | `FluentClip.Events` | 事件驱动通信 |

---

## 🔌 插件接口

### IPlugin - 基础插件接口

```csharp
using FluentClip.Core.API;

public class MyPlugin : IPlugin
{
    public string Id => "com.example.myplugin";
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Author => "开发者名称";
    public string Description => "插件描述";

    public void Initialize(IPluginContext context)
    {
        // 插件初始化
    }

    public void Shutdown()
    {
        // 插件关闭时清理资源
    }
}
```

### IPluginContext - 插件上下文

插件通过上下文获取应用服务：

```csharp
public interface IPluginContext
{
    // 服务注册与获取
    void RegisterService(Type serviceType, object instance);
    T GetService<T>() where T : class;

    // 事件系统
    void SubscribeToEvent<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs;
    void PublishEvent<TEvent>(TEvent eventArgs) where TEvent : EventArgs;

    // 功能扩展
    void RegisterClipboardItemProcessor(IClipboardItemProcessor processor);
    void RegisterDragDropHandler(IDragDropHandler handler);
    void RegisterThumbnailGenerator(IThumbnailGenerator generator);

    // 菜单扩展
    void AddMenuItem(string menuId, string header, RoutedEventHandler clickHandler);
    void AddContextMenuItem(string targetMenuId, string header, RoutedEventHandler clickHandler);
}
```

---

## 💻 开发示例

### 示例 1：自定义缩略图生成器

为特定文件类型创建缩略图：

```csharp
using System;
using System.Windows.Media.Imaging;
using FluentClip.Core.API;

public class MyThumbnailGenerator : IThumbnailGenerator
{
    public string GeneratorId => "my-custom-thumbnail";
    public int Priority => 100;  // 更高优先级先执行
    public string[] SupportedExtensions => new[] { ".myformat" };

    public bool CanGenerate(string filePath)
    {
        return filePath.EndsWith(".myformat", StringComparison.OrdinalIgnoreCase);
    }

    public BitmapSource? Generate(string filePath, int width, int height)
    {
        // 实现你的缩略图生成逻辑
        // 返回 null 则使用默认处理
        return null;
    }
}
```

### 示例 2：剪贴板处理器

处理特定类型的剪贴内容：

```csharp
using System.Windows.Media.Imaging;
using FluentClip.Core.API;
using FluentClip.Models;

public class MyClipboardProcessor : IClipboardItemProcessor
{
    public string ProcessorId => "my-clipboard-processor";
    public int Priority => 50;

    public bool CanProcess(ClipboardItem item)
    {
        // 判断是否可以处理
        return item.ItemType == ClipboardItemType.File &&
               item.FilePaths?[0]?.EndsWith(".special") == true;
    }

    public ClipboardItem Process(ClipboardItem item)
    {
        // 处理剪贴板项
        // 可以修改 item 的属性
        return item;
    }

    public BitmapSource? GenerateThumbnail(string filePath, int width, int height)
    {
        // 自定义缩略图生成
        return null;
    }
}
```

### 示例 3：拖拽处理器

自定义拖拽行为：

```csharp
using System;
using System.Windows;
using FluentClip.Core.API;

public class MyDragDropHandler : IDragDropHandler
{
    public string HandlerId => "my-drag-handler";
    public int Priority => 50;

    public bool CanHandle(DragDropInfo info)
    {
        // 判断是否可以处理
        return info.FilePaths?.Length > 0;
    }

    public void HandleDragEnter(DragDropInfo info)
    {
        // 拖入处理
    }

    public void HandleDragLeave(DragDropInfo info)
    {
        // 拖出处理
    }

    public void HandleDrop(DragDropInfo info)
    {
        // 放置处理
    }

    public DragDropEffects GetDropEffect(DragDropInfo info)
    {
        return DragDropEffects.Copy;
    }
}
```

### 示例 4：事件订阅

监听应用事件：

```csharp
using System;
using FluentClip.Events;

public class MyEventListener
{
    public void Subscribe(IPluginContext context)
    {
        // 订阅剪贴板项创建事件
        context.SubscribeToEvent<ClipboardItemCreatedEvent>(OnItemCreated);

        // 订阅拖拽进入事件
        context.SubscribeToEvent<DragEnterEvent>(OnDragEnter);
    }

    private void OnItemCreated(object? sender, ClipboardItemCreatedEvent e)
    {
        Console.WriteLine($"新剪贴板项: {e.Item.DisplayText}");
    }

    private void OnDragEnter(object? sender, DragEnterEvent e)
    {
        Console.WriteLine($"文件拖入: {string.Join(", ", e.FilePaths)}");
    }
}
```

---

## 🚀 高级功能

### 事件类型参考

| 事件 | 说明 |
|------|------|
| `ClipboardItemCreatedEvent` | 剪贴板项创建时 |
| `ClipboardItemDeletedEvent` | 剪贴板项删除时 |
| `ClipboardItemCopiedEvent` | 剪贴板项复制时 |
| `DragEnterEvent` | 拖拽进入时 |
| `DragLeaveEvent` | 拖拽离开时 |
| `DropEvent` | 放置时 |
| `ToastRequestEvent` | 请求显示通知 |

### 菜单扩展

```csharp
// 添加主菜单项
context.AddMenuItem("main", "我的插件", OnMenuClick);

// 添加右键菜单项
context.AddContextMenuItem("item", "处理此项", OnContextMenuClick);
```

### 服务获取

```csharp
// 获取主窗口
var mainWindow = context.GetService<MainWindow>();

// 获取 ViewModel
var viewModel = context.GetService<MainViewModel>();

// 获取设置
var settings = context.GetService<AppSettings>();
```

---

## 🐛 调试与发布

### 调试模式

```bash
# 调试运行
dotnet run --project FluentClip

# 或者在 Visual Studio 中按 F5
```

### 插件加载

当前插件需要编译到主应用中：

```csharp
// 在 PluginManager 中注册插件
public void Initialize()
{
    // 方式 1：直接注册
    LoadPlugin(new MyPlugin());

    // 方式 2：从程序集加载
    LoadPluginsFromAssembly("MyPlugin.dll");
}
```

### 发布插件

1. 创建一个新的 C# 类库项目
2. 引用 FluentClip.Core.API
3. 实现所需的接口
4. 编译为 DLL
5. 将 DLL 放置到插件目录

---

## 📚 相关文档

- 📖 [README](README.md) - 项目概览
- 🤝 [贡献指南](CONTRIBUTING.md) - 参与开发
- 🏗️ [核心架构](./Core/) - 架构详解

---

## ❓ 获取帮助

- 💬 [GitHub Discussions](https://github.com/fluentclip/fluentclip/discussions)
- 🐛 [报告问题](https://github.com/fluentclip/fluentclip/issues)

---

<p align="center">
  欢迎创建插件，让 FluentClip 更强大！ 🚀
</p>
