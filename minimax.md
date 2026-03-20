# 🔄 FluentClip 开发接力文档

> 本文档供下一个 AI 助手接力开发使用，记录开发思路、踩坑记录和待办事项。

---

## 📊 项目当前状态

| 项目 | 状态 |
|:---|:---|
| 核心功能 | ✅ 基本完成 |
| 剪贴板监控 | ✅ 完成 |
| 文件拖拽 | ✅ 完成 |
| 垃圾桶删除 | ✅ 完成 |
| AI 助手 | ✅ 基本完成 |
| 系统托盘 | ✅ 完成 |
| 全局热键 | ✅ 完成 |
| 插件系统 | 🔄 框架搭建完成 |
| 缩略图优化 | ✅ 完成 |
| UI 美化 | 🔄 进行中 |

---

## 🐛 踩坑记录 (Pitfalls)

### 1. WPF 拖拽事件不触发

**问题**: Window 级别的 DragEnter/DragOver/Drop 事件不触发

**原因**: 子元素先捕获了拖拽事件

**解决方案**: 使用 Window 本身的事件，并在 XAML 中设置 AllowDrop="True"

```csharp
// 在 MainWindow.xaml.cs 中
Window_DragEnter(object sender, DragEventArgs e)
{
    // 使用 e.GetPosition(this) 获取相对于窗口的坐标
    // 而非 e.GetPosition(sender as UIElement)
}
```

---

### 2. Toggle Switch 白点偏移

**问题**: 自定义 ToggleButton 样式的白色滑块与绿色背景偏移

**原因**: 使用 Margin/HorizontalAlignment 动画导致位置不同步

**解决方案**: 使用 TranslateTransform 动画

```xml
<!-- 正确方式 -->
<Thumb.RenderTransform>
    <TranslateTransform x:Name="thumbTransform"/>
</Thumb.RenderTransform>

<!-- 动画使用 -->
<DoubleAnimation Storyboard.TargetName="thumbTransform"
                Storyboard.TargetProperty="X"
                To="14" Duration="0:0:0.15"/>
```

---

### 3. 缩略图圆形裁剪内容截断

**问题**: 40x40 圆形裁剪显示不全系统图标

**解决方案**: 生成 96x96 大图，显示时缩放到 40x40

```csharp
// 生成时使用大尺寸
thumbnail = ThumbnailHelper.GenerateThumbnail(filePath, 96, 96);

// 显示时使用小尺寸
<Grid Width="40" Height="40">
    <Image Stretch="UniformToFill">
        <Image.Clip>
            <EllipseGeometry Center="20,20" RadiusX="20" RadiusY="20"/>
        </Image.Clip>
    </Image>
</Grid>
```

---

### 4. 拖拽文件到外部失效

**问题**: 从列表拖拽文件到外部程序（如桌面）失败

**原因**: DragDrop.DoDragDrop 时没有正确设置 DataObject

**解决方案**: 同时设置多种数据格式

```csharp
var dataObject = new DataObject();
dataObject.SetData(DataFormats.FileDrop, filePaths);
dataObject.SetData(DataFormats.UnicodeText, filePaths[0]); // 备选
DragDrop.DoDragDrop(border, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
```

---

### 5. 元素级 DragEvent 不触发

**问题**: 给 Border 设置 Drop 事件但从不触发

**原因**: 拖拽事件被父窗口拦截

**解决方案**: 使用 Window 级别事件 + 坐标检测判断是否在目标区域

```csharp
// Window 级别
this.DragEnter += Window_DragEnter;
this.Drop += Window_Drop;

// 判断是否在垃圾桶区域
var trashBounds = VisualTreeHelper.GetDescendantBounds(TrashBorder);
var trashTopLeft = TrashBorder.TransformToAncestor(this).Transform(new Point(0, 0));
var trashRect = new Rect(trashTopLeft, trashBounds.Size);

if (trashRect.Contains(e.GetPosition(this)))
{
    // 在垃圾桶区域
}
```

---

## 📋 待办事项 (Todo)

### 高优先级

| 任务 | 说明 | 状态 |
|:---|:---|:---|
| 完善插件系统 | PluginManager 实际加载逻辑 | 🔄 待开发 |
| **持久化存储** | **剪贴板历史和AI对话保存到本地** | 🔄 待开发 |
| 国际化 | 中英文语言切换 | 🔄 待开发 |
| 设置界面优化 | 更多配置项 | 🔄 待开发 |

### 中优先级

| 任务 | 说明 | 状态 |
|:---|:---|:---|
| 主题切换 | 浅色/深色主题 | 🔄 待开发 |
| 快捷键自定义 | 用户可配置热键 | 🔄 待开发 |
| 数据导入导出 | 备份/恢复功能 | 🔄 待开发 |
| AI 对话历史 | 保存 AI 对话记录 | 🔄 待开发 |

---

## 💾 数据存储现状

### 已持久化

| 数据 | 路径 |
|:---|:---|
| 应用设置 | `%APPDATA%\FluentClip\settings.json` |
| AI 设置 | `%APPDATA%\FluentClip\agent_settings.json` |

### 内存中 (未持久化)

| 数据 | 说明 |
|:---|:---|
| **剪贴板历史** | 暂存区文件/文本/图片，仅在内存中，关闭应用后丢失 |
| **AI 对话历史** | 对话上下文仅在内存中，关闭应用后丢失 |

### 待实现

- 剪贴板历史持久化到本地文件夹
- AI 对话历史持久化保存
- 建议：创建 `%APPDATA%\FluentClip\staging\` 文件夹作为暂存区

### 低优先级

| 任务 | 说明 | 状态 |
|:---|:---|:---|
| 插件市场 | 内置插件下载 | 🔄 待规划 |
| 云同步 | 跨设备同步设置 | 🔄 待规划 |
| 快捷命令 | 输入 /xxx 执行命令 | 🔄 待规划 |

---

## 🏗️ 架构说明

### 当前目录结构

```
FluentClip/
├── Core/                    # 核心系统
│   ├── API/                 # 插件接口
│   │   ├── IPlugin.cs
│   │   ├── IClipboardItemProcessor.cs
│   │   ├── IDragDropHandler.cs
│   │   └── IThumbnailGenerator.cs
│   └── PluginManager.cs    # 插件管理器
├── Events/                  # 事件系统
│   ├── EventAggregator.cs
│   └── ClipboardEvents.cs
├── Factories/              # 工厂模式
│   └── ClipboardItemFactory.cs
├── Models/                 # 数据模型
│   ├── ClipboardItem.cs
│   ├── AppSettings.cs
│   └── AgentSettings.cs
├── Services/               # 业务服务
│   ├── ClipboardService.cs
│   ├── AgentService.cs
│   ├── HotkeyManager.cs
│   ├── ThumbnailHelper.cs
│   ├── ToastService.cs    # Toast 通知
│   ├── WindowService.cs   # 窗口管理
│   └── DragDropService.cs # 拖拽服务
├── ViewModels/
│   └── MainViewModel.cs
├── MainWindow.xaml(.cs)   # 主窗口
├── SettingsWindow.xaml(.cs)
├── AgentSettingsWindow.xaml(.cs)
└── App.xaml
```

### 关键技术决策

| 决策 | 理由 |
|:---|:---|
| MVVM 架构 | WPF 标准模式，便于测试和维护 |
| 事件聚合器 | 组件间松耦合通信 |
| 工厂模式 | 统一创建 ClipboardItem |
| 服务分离 | Toast/Window/DragDrop 独立服务 |

---

## 🔧 开发规范

### 代码风格

- 使用 **var** 推断类型
- 不添加多余注释（除非复杂逻辑）
- XML 文档注释用于公开 API
- 使用 C# 12 新特性

### Git 提交规范

```
feat: 新功能
fix: Bug 修复
docs: 文档更新
style: 格式调整
refactor: 代码重构
```

---

## 📝 常用命令

```bash
# 构建
dotnet build

# 运行
dotnet run

# 发布 (单文件)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 🔗 相关链接

- MiniMax API: https://platform.minimaxi.com
- .NET 9 Docs: https://learn.microsoft.com/zh-cn/dotnet/
- WPF Docs: https://learn.microsoft.com/zh-cn/dotnet/desktop/wpf/

---

## 📅 更新日志

| 日期 | 内容 |
|:---|:---|
| 2024-03-20 | 初始化项目，添加核心功能 |
| 2024-03-20 | 添加拖拽、垃圾桶功能 |
| 2024-03-20 | 美化设置界面 ToggleSwitch |
| 2024-03-20 | 优化缩略图显示 (40x40/96x96) |
| 2024-03-20 | 添加插件系统架构 |
| 2024-03-20 | 更新文档 (README/CONTRIBUTING/PLUGIN) |
| 2024-03-20 | 提交到 GitHub |

---

<p align="center">
  <sub>继续加油！🚀</sub>
</p>
