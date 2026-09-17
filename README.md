# UE Plugin Compiler

[![.NET 构建与发布](https://github.com/Azureusbin/UE_Plugin_Compiler/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/Azureusbin/UE_Plugin_Compiler/actions/workflows/dotnet.yml)

一款 Windows 桌面工具，用于跨多个 Unreal Engine 版本批量编译插件，支持可配置的编译流程、构建后处理步骤和实时日志输出。

![流程编辑器主界面](Doc/Images/Primary%20Interface.png)

*流程编辑器：任务卡片显示每个任务使用的引擎与插件，UAT 日志按级别着色实时输出，底部状态栏跟踪当前进度（`Running Task: RuntimeAudioImporter → UE 5.8 (2/3)`），运行期间按钮切换为 **Cancel**。*

## 功能

- 🔍 **自动检测 UE 安装** — 扫描 Windows 注册表（64 位 + 32 位）和文件系统（`C:\`、`D:\Program Files\Epic Games`）
- ✋ **自定义引擎路径** — 通过设置页面添加任意目录下的源码构建版本
- 📋 **BuildFlow 流程系统** — 定义多任务编译管线，支持保存/加载 `.uflow` 文件
- 🔀 **批量编译** — 单次运行即可完成 N 个插件 × N 个引擎 × M 个任务的编译 （顺序执行）
- 🧹 **构建后处理** — 在最终输出前对打包产物执行删除（glob 匹配）、复制或运行 `.bat`/`.cmd` 脚本
- 🔧 **环境变量注入** — 按任务注入环境变量（如 `EDITORCRYPT_BUILD_MODE`）
- 🎨 **日志着色** — 可在设置页面自定义错误/警告/成功/普通日志的颜色
- 📊 **实时输出** — 实时流式显示 UAT 构建日志及进度
- 📁 **结构化输出** — 结果按 `{引擎版本}/{任务名称}/{插件名称}/` 组织
- 🪟 **深色主题** — UE 风格配色 + 自定义标题栏
- ✅ **完成弹窗** — 构建摘要包含成功/失败计数和"打开输出目录"按钮
- 💾 **便携运行** — 单文件 `.exe`，所有数据保存在 `Saved/` 目录

## 系统要求

- Windows 10 / 11（64 位）
- .NET 10 SDK（使用自包含 exe 则无需安装）
- 至少已安装一个 Unreal Engine 版本

## 下载

前往 [Releases](https://github.com/Azureusbin/UE_Plugin_Compiler/releases) 下载最新的单文件免安装版（`UEPluginCompiler-win64-vX.Y.Z.zip`），解压即用，无需安装 .NET 运行时。

## 快速开始

1. 启动 `UEPluginCompiler.exe`
2. 点击 **新建 BuildFlow**（或打开最近的 `.uflow` 文件）
3. 点击 **+ 添加任务** — 选择引擎、选取 `.uplugin`、配置环境变量和构建后处理步骤
4. 为每种构建配置重复上述步骤
5. 设置输出目录 → **运行全部任务**

## 界面概览

| 页面 | 用途 |
|------|------|
| **欢迎页** | 新建 / 打开 / 最近使用的 BuildFlow |
| **流程编辑器** | 任务列表、输出目录、全部运行、实时日志面板、取消 |
| **任务编辑器** | 按任务选择引擎、插件、环境变量、构建后处理步骤、Clean 构建开关 |
| **设置页** | 引擎管理、日志颜色自定义（标题栏 ⚙ 图标） |

### 设置页

![设置页 — 引擎管理与日志配色](Doc/Images/Settings%20Panel.png)

*引擎列表包含版本 / 显示名 / 路径 / 来源 / 状态（来源区分 `registry` 与 `filesystem`，指向已删除目录的失效项会被过滤），状态栏显示检测到的引擎数量；下方可为 Error、Warning、Success、Normal 四级日志分别配色，或一键 **Reset Defaults** 恢复默认。*

## BuildFlow 文件（`.uflow`）

JSON 格式，camelCase 键名，可存放在任意位置：

```json
{
  "name": "EditorCrypt 全模式编译",
  "outputDir": "F:/Builds/EditorCrypt",
  "tasks": [
    {
      "name": "AllOn",
      "enginePaths": ["C:/Program Files/Epic Games/UE_5.7"],
      "pluginPath": "F:/MyPlugin/MyPlugin.uplugin",
      "outputDir": "",
      "envVars": { "EDITORCRYPT_BUILD_MODE": "AllOn" },
      "noP4": true,
      "cleanBuild": false,
      "postBuildSteps": [
        { "type": "delete", "pattern": "**/Private/*.cpp" },
        { "type": "delete", "pattern": "**/Public/*.h" },
        { "type": "copy",  "pattern": "Binaries/**/*.dll", "destination": "Stripped/" },
        { "type": "run",   "pattern": "strip_pdb.bat" }
      ]
    }
  ]
}
```

| 字段（任务级） | 类型 | 说明 |
|------|------|------|
| `outputDir` | `string` | 按任务覆盖输出目录；为空则回退到流程级 `outputDir` |
| `cleanBuild` | `bool` | 向 UAT 传递 `-Clean` 以执行完整重新构建 |
| `noP4` | `bool` | 向 UAT 传递 `-NoP4`（默认 `true`） |
| `postBuildSteps` | `array` | 对打包输出执行的删除、复制或运行步骤（见下文） |

### 构建后处理步骤

步骤在 UAT 构建**成功后**、将结果复制到最终输出目录**之前**，在中间打包目录上执行。

| 类型 | `pattern` | `destination` | 行为 |
|------|-----------|---------------|------|
| `"delete"` | Glob（如 `**/*.pdb`） | — | 删除匹配的文件；0 匹配 → 仅警告；执行后清理空目录 |
| `"copy"` | Glob | 相对于包根目录的路径 | 将匹配文件复制到目标目录；0 匹配 → **致命错误** |
| `"run"` | `.bat`/`.cmd` 路径 | — | 执行脚本，注入 `PACKAGE_DIR`、`PLUGIN_DIR`、`ENGINE_VERSION`、`ENGINE_DIR`、`TASK_NAME`、`OUTPUT_DIR` 环境变量；非零退出码 → **致命错误** |

`pattern` 和 `destination` 中的路径相对于打包后的插件根目录。对于 run 步骤，脚本路径优先在插件源码目录中解析（这样 `.bat` 可以和 `.uplugin` 放在一起），其次在打包目录中解析。请使用正斜杠。

## 目录结构

```
UEPluginCompiler.exe
Saved/
├── settings.json           # 最近使用的流程列表 + 日志颜色配置
├── custom_engines.json     # 用户手动添加的引擎路径
├── debug.log               # 仅 Debug 构建时输出
└── logs/
    └── 2026-07-17_143052/
        └── _summary.log
```

## 从源码构建

```bash
# 前置条件：.NET 10 SDK
dotnet build UEPluginCompiler

# 运行（Debug）
dotnet run --project UEPluginCompiler

# 发布单文件 exe
dotnet publish UEPluginCompiler -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

# 或在 Windows 上直接运行：publish.bat
```

## 持续集成与自动发布

仓库配置了 GitHub Actions（[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)），采用 **build → release** 两段式流水线：

| 阶段 | 触发条件 | 主要动作 |
|------|----------|----------|
| `build` | push / PR 到 `master` | 还原依赖（NuGet 缓存按 `.csproj` 哈希命中）→ `dotnet publish` 产出 win-x64 单文件自包含 exe → 上传 artifact |
| `release` | 仅 push 到 `master` | 下载 artifact → 从 `.csproj` 读取 `Version` 作为 tag → 打包 zip（含 exe / README / LICENSE）→ 自动创建 Release 并上传产物 |

设计要点：

- **版本号单一来源**——tag 名直接取自 `UEPluginCompiler.csproj` 的 `Version` 属性，发版只需改一处
- **发布参数集中在 `.csproj`**——`PublishSingleFile` / `SelfContained` / `EnableCompressionInSingleFile` 写在项目文件中，CI 的 `dotnet publish` 无需重复传参；本地 `publish.bat` 显式传入相同参数，两条路径产出形态一致
- **跨 job 产物传递**——`release` 通过 `needs: build` 依赖构建阶段，用 artifact 传递产物而非重复编译
- **最小权限**——`release` job 仅申请 `contents: write`，用于创建 Release
- **全自动**——Release 与产物均由 CI 自动创建，无需人工干预；产物附 SHA256 校验值，可追溯来源

发布新版本：修改 `.csproj` 中的 `Version` → 提交并推送到 `master`，其余交给 CI。

## 工作原理

### UE 检测
1. **注册表**：`HKLM\SOFTWARE\EpicGames\Unreal Engine`（64 位 + 32 位视图）
2. **文件系统**：`C:\Program Files\Epic Games\UE_*`、`C:\Program Files (x86)\Epic Games\UE_*`、`D:\Program Files\Epic Games\UE_*`
3. **自定义路径**：通过设置页面添加，持久化到 `Saved/custom_engines.json`

所有条目均通过 `Engine\Build\BatchFiles\RunUAT.bat` 是否存在进行验证。版本号从 `Engine\Build\Build.version` JSON 中读取（回退到目录名）。指向已删除目录的失效注册表项会被静默过滤。

### 编译流程
1. 对每个任务 → 每个选中的引擎：
   - 将环境变量写入进程
   - 通过 `RunUAT.bat BuildPlugin` 编译到临时目录（避免路径过长问题）
   - 在临时打包目录上执行构建后处理步骤（如有）
   - 将结果复制到 `{输出目录}/{引擎版本}/{任务名称}/{插件名称}/`
2. 实时流式输出 UAT 进程的 stdout/stderr
3. 完成后：显示每个任务每个引擎的 ✅/❌ 摘要及总耗时

![构建完成摘要](Doc/Images/Compile%20Result.png)

*构建完成摘要：按任务分组列出每个引擎的 ✅/❌ 结果、退出码与耗时，末尾汇总成功/失败总数与总耗时；同样的内容会写入 `Saved/logs/<时间戳>/_summary.log`。*

### 取消编译
点击 **取消** 会终止当前 UAT 进程（整个进程树），跳过剩余任务，并清理中间目录。已完成的任务会被保留。

## 项目结构

```
UEPluginCompiler/
├── Models/
│   ├── BuildFlow.cs          # BuildFlow、BuildTask、PostBuildStep
│   ├── BuildResult.cs        # BuildResult 记录
│   ├── CompileRequest.cs     # CompileRequest 记录
│   └── EngineInstall.cs      # EngineInstall（INotifyPropertyChanged）
├── Services/
│   ├── UEDetector.cs         # 注册表 + 文件系统引擎检测
│   ├── PluginCompiler.cs     # RunUAT.BuildPlugin 执行与取消
│   ├── PostBuildRunner.cs    # 构建后处理 delete/copy/run 步骤执行器
│   ├── FlowSerializer.cs     # .uflow JSON 读写
│   └── SettingsManager.cs    # AppSettings + 自定义引擎持久化
├── ViewModels/
│   ├── WelcomeViewModel.cs   # 最近流程、新建/打开操作
│   ├── FlowEditorViewModel.cs # 任务增删改、全部运行编排、日志
│   ├── TaskEditorViewModel.cs # 按任务编辑引擎/环境变量/构建后处理
│   ├── SettingsViewModel.cs  # 引擎管理 + 日志颜色自定义
│   └── RelayCommand.cs       # RelayCommand + AsyncRelayCommand
├── Views/
│   ├── WelcomePage.xaml/.cs
│   ├── FlowEditorPage.xaml/.cs
│   ├── TaskEditorDialog.xaml/.cs
│   ├── SettingsPage.xaml/.cs
│   ├── CompletionDialog.xaml/.cs  # 构建完成摘要弹窗
│   └── DarkDialog.cs             # 深色主题 MessageBox 替代
├── Helpers/
│   ├── Logger.cs             # Debug 模式下的文件日志
│   ├── PathValidator.cs      # .uplugin 验证、版本号提取
│   └── FolderBrowser.cs      # COM IFileDialog 封装（无 WinForms 依赖）
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── App.xaml                  # 深色主题资源、全局样式
├── App.xaml.cs
└── MainWindow.xaml/.cs       # 自定义标题栏、页面导航
```

## 许可证

本项目采用 [MIT License](LICENSE) 开源。

## 作者

**AzureusBin**

---

> 如果这个工具有帮到你，欢迎点个 Star ⭐
