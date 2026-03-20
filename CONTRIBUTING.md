# 🤝 贡献指南

<p align="center">
  <img src="https://img.shields.io/badge/We%20Welcome%20Contributions!-green?style=for-the-badge">
</p>

感谢您对 FluentClip 的兴趣！我们欢迎任何形式的贡献，包括但不限于：

- 🐛 报告 Bug
- 💡 提出新功能建议
- 📝 完善文档
- 💻 提交代码修复或新功能
- 🌐 翻译文档

---

## 📋 目录

1. [行为准则](#-行为准则)
2. [如何贡献](#-如何贡献)
3. [开发环境设置](#-开发环境设置)
4. [代码规范](#-代码规范)
5. [提交Pull Request](#-提交pull-request)
6. [项目结构说明](#-项目结构说明)

---

## 📜 行为准则

我们希望营造一个友好、包容的社区。

> ⭐ **核心原则**：尊重、包容、开放

---

## 🚀 如何贡献

### 步骤 1：Fork 仓库

点击 GitHub 页面右上角的 **Fork** 按钮。

### 步骤 2：克隆仓库

```bash
git clone https://github.com/YOUR_USERNAME/FluentClip.git
cd FluentClip
```

### 步骤 3：创建分支

```bash
# 创建功能分支
git checkout -b feature/your-feature-name

# 创建修复分支
git checkout -b fix/bug-description
```

### 步骤 4：开发与测试

```bash
# 安装依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试（如有）
dotnet test
```

### 步骤 5：提交更改

```bash
# 添加更改
git add .

# 提交（使用语义化提交信息）
git commit -m "feat: 添加新功能描述"

# 推送到你的 Fork
git push origin feature/your-feature-name
```

### 步骤 6：创建 Pull Request

1. 访问原始仓库
2. 点击 **New Pull Request**
3. 填写 PR 描述
4. 等待代码审查

---

## 🛠️ 开发环境设置

### 环境要求

| 要求 | 版本 |
|------|------|
| 操作系统 | Windows 10/11 |
| .NET SDK | 9.0+ |
| IDE | Visual Studio 2022+ / Rider |

### 推荐 VS Code 插件

- **C# Dev Kit** - C# 开发支持
- **XAML Language Server** - XAML 智能提示
- **GitLens** - Git 增强
- **Prettier** - 代码格式化

### 本地运行

```bash
# 调试模式运行
dotnet run

# 发布构建
dotnet publish -c Release
```

---

## 📝 代码规范

### C# 编码规范

- ✅ 使用 **C# 12** 新特性（如原始字符串字面量）
- ✅ 使用 **var** 推断类型
- ✅ 命名遵循 Microsoft 命名约定
- ✅ 添加必要的 XML 文档注释

### XAML 规范

- ✅ 使用有意义的名称（Name 属性）
- ✅ 资源字典按功能模块组织
- ✅ 遵循 MVVM 模式，避免代码后置中的业务逻辑

### Git 提交规范

我们使用 **语义化提交** 格式：

```
<type>(<scope>): <subject>

<body>

<footer>
```

#### 类型 (type)

| 类型 | 描述 |
|------|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档更新 |
| `style` | 代码格式调整 |
| `refactor` | 代码重构 |
| `test` | 测试相关 |
| `chore` | 构建/工具 |

#### 示例

```bash
# 功能
git commit -m "feat(clipboard): 添加文件拖拽支持"

# 修复
git commit -m "fix(thumbnail): 修复大图片缩略图不显示"

# 文档
git commit -m "docs(readme): 更新项目架构说明"
```

---

## 🔄 提交 Pull Request

### PR 标题规范

```
[类型] 简短描述

例如：
[Feature] 添加自定义缩略图生成器
[Bugfix] 修复拖拽删除失效问题
```

### PR 描述模板

```markdown
## 概述
简要描述这个 PR 解决的问题或添加的功能。

## 变更内容
- 变更 1
- 变更 2
- 变更 3

## 测试
- [ ] 本地测试通过
- [ ] 添加了必要的单元测试

## 截图（如适用）
[在此添加 UI 变更的截图]
```

### 审查要点

PR 将会从以下方面进行审查：

1. ✅ **功能正确性** - 代码是否按预期工作
2. ✅ **代码质量** - 是否符合项目规范
3. ✅ **性能影响** - 是否有性能问题
4. ✅ **测试覆盖** - 是否有足够的测试
5. ✅ **文档更新** - 是否需要更新文档

---

## 📂 项目结构说明

```
FluentClip/
├── 📂 Core/           # 核心系统（插件、事件）
├── 📂 Events/         # 事件聚合器
├── 📂 Factories/     # 工厂模式
├── 📂 Models/         # 数据模型
├── 📂 Services/      # 业务服务
├── 📂 ViewModels/    # MVVM 视图模型
├── MainWindow.xaml   # 主窗口
└── *.xaml            # 其他窗口
```

### 核心模块

| 模块 | 说明 |
|------|------|
| `Core/API` | 插件接口定义 |
| `Core/PluginManager` | 插件生命周期管理 |
| `Events/EventAggregator` | 组件间通信 |
| `Factories/ClipboardItemFactory` | 剪贴板项创建 |

### 服务层

| 服务 | 职责 |
|------|------|
| `ClipboardService` | 剪贴板监控 |
| `AgentService` | AI 助手 |
| `HotkeyManager` | 全局热键 |
| `ThumbnailHelper` | 缩略图生成 |

---

## ❓ 获取帮助

- 📖 [项目文档](README.md)
- 🔌 [插件开发指南](PLUGIN_DEVELOPMENT.md)
- 💬 [GitHub Discussions](https://github.com/fluentclip/fluentclip/discussions)
- 🐛 [报告问题](https://github.com/fluentclip/fluentclip/issues)

---

<p align="center">
  感谢您的贡献！ 🎉
</p>
