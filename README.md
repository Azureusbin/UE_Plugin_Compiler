# UE Plugin Compiler

A Windows desktop tool for batch-compiling Unreal Engine plugins across multiple engine versions with configurable build flows, post-build steps, and real-time log streaming.

## Features

- 🔍 **Auto-detects UE installations** — scans Windows registry (64-bit + 32-bit) and filesystem (`C:\`, `D:\Program Files\Epic Games`)
- ✋ **Custom engine paths** — add source builds from any folder via the Settings page
- 📋 **BuildFlow system** — define multi-task pipelines, save/load `.uflow` files
- 🔀 **Batch compilation** — compile one plugin × N engines × M tasks in a single run
- 🧹 **Post-build steps** — delete globs, copy files, or run `.bat`/`.cmd` scripts on the packaged plugin before it lands in the final output
- 🔧 **Environment variables** — inject env vars per task (e.g. `EDITORCRYPT_BUILD_MODE`)
- 🎨 **Log coloring** — customizable error/warning/success/normal colors (Settings page)
- 📊 **Real-time output** — live streaming UAT log with progress
- 📁 **Structured output** — results organized by `{EngineVersion}/{TaskName}/{PluginName}/`
- 🪟 **Dark theme** — UE-inspired palette with a custom title bar
- ✅ **Completion dialog** — summary with succeeded/failed counts and one-click "Open Output Folder"
- 💾 **Portable** — single `.exe`, all data in `Saved/` folder

## Requirements

- Windows 10 / 11 (64-bit)
- .NET 10 SDK (or use the self-contained exe)
- At least one Unreal Engine installation

## Quick Start

1. Launch `UEPluginCompiler.exe`
2. Click **New BuildFlow** (or open a recent `.uflow`)
3. Click **+ Add Task** — pick engines, select `.uplugin`, set env vars and post-build steps
4. Repeat for each build configuration
5. Set output directory → **Run All Tasks**

## Screens

| Screen | Purpose |
|--------|---------|
| **Welcome** | New / Open / Recent BuildFlows |
| **Flow Editor** | Task list, output dir, Run All, live log panel, cancel |
| **Task Editor** | Per-task engine selection, plugin, env vars, post-build steps, clean build toggle |
| **Settings** | Engine management, log color customization (⚙ in title bar) |

## BuildFlow Files (`.uflow`)

JSON format with camelCase keys, stored wherever you choose:

```json
{
  "name": "EditorCrypt All Modes",
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

| Field (task-level) | Type | Description |
|-----|------|-------------|
| `outputDir` | `string` | Per-task override; falls back to the flow-level `outputDir` if empty |
| `cleanBuild` | `bool` | Pass `-Clean` to UAT for a full rebuild |
| `noP4` | `bool` | Pass `-NoP4` to UAT (default `true`) |
| `postBuildSteps` | `array` | Delete, copy, or run steps executed on the packaged output (see below) |

### Post-Build Steps

Steps run on the intermediate package directory **after** a successful UAT build and **before** the result is copied to the final output folder.

| Type | `pattern` | `destination` | Behavior |
|------|-----------|---------------|----------|
| `"delete"` | Glob (e.g. `**/*.pdb`) | — | Deletes matched files; 0 matches → warning only; prunes empty dirs after |
| `"copy"` | Glob | Dir relative to package root | Copies matched files into the destination; 0 matches → **fatal error** |
| `"run"` | `.bat`/`.cmd` path | — | Executes script with `PACKAGE_DIR`, `PLUGIN_DIR`, `ENGINE_VERSION`, `ENGINE_DIR`, `TASK_NAME`, `OUTPUT_DIR` env vars; non-zero exit → **fatal error** |

Paths in `pattern` and `destination` are relative to the packaged plugin root. For run steps, the script path resolves against the plugin source directory first (so the `.bat` can live next to the `.uplugin`), then the package directory. Use forward slashes.

## Directory Layout

```
UEPluginCompiler.exe
Saved/
├── settings.json           # Recent flows list + log colors
├── custom_engines.json     # User-added engine paths
├── debug.log               # Debug build only
└── logs/
    └── 2026-07-17_143052/
        └── _summary.log
```

## Build from Source

```bash
# Prerequisites: .NET 10 SDK
dotnet build UEPluginCompiler

# Run (Debug)
dotnet run --project UEPluginCompiler

# Publish single-file exe
dotnet publish UEPluginCompiler -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

# Or on Windows: publish.bat
```

## How It Works

### UE Detection
1. **Registry**: `HKLM\SOFTWARE\EpicGames\Unreal Engine` (64-bit + 32-bit views)
2. **Filesystem**: `C:\Program Files\Epic Games\UE_*`, `C:\Program Files (x86)\Epic Games\UE_*`, `D:\Program Files\Epic Games\UE_*`
3. **Custom paths**: added via Settings page, persisted to `Saved/custom_engines.json`

All entries are validated against the presence of `Engine\Build\BatchFiles\RunUAT.bat`. Version numbers are read from `Engine\Build\Build.version` JSON (falling back to the directory name). Stale registry entries pointing to deleted folders are silently filtered out.

### Compilation
1. For each Task → each selected Engine:
   - Write env vars into the process
   - Compile to a temp directory via `RunUAT.bat BuildPlugin` (avoids path-length issues)
   - Run post-build steps on the temp package (if any)
   - Copy the result to `{OutputDir}/{EngineVersion}/{TaskName}/{PluginName}/`
2. Live stdout/stderr streamed from the UAT process
3. On completion: summary with ✅/❌ per task per engine + total elapsed time

### Cancellation
Clicking **Cancel** kills the current UAT process (entire process tree), skips remaining tasks, and cleans up intermediate directories. Already-completed tasks are preserved.

## Project Structure

```
UEPluginCompiler/
├── Models/
│   ├── BuildFlow.cs          # BuildFlow, BuildTask, PostBuildStep
│   ├── BuildResult.cs        # BuildResult record
│   ├── CompileRequest.cs     # CompileRequest record
│   └── EngineInstall.cs      # EngineInstall (INotifyPropertyChanged)
├── Services/
│   ├── UEDetector.cs         # Registry + filesystem engine detection
│   ├── PluginCompiler.cs     # RunUAT.BuildPlugin execution with cancellation
│   ├── PostBuildRunner.cs    # Post-build delete/copy/run step executor
│   ├── FlowSerializer.cs     # .uflow JSON read/write
│   └── SettingsManager.cs    # AppSettings + custom engines persistence
├── ViewModels/
│   ├── WelcomeViewModel.cs   # Recent flows, new/open actions
│   ├── FlowEditorViewModel.cs # Task CRUD, Run All orchestration, logging
│   ├── TaskEditorViewModel.cs # Per-task engine/env/post-build editing
│   ├── SettingsViewModel.cs  # Engine management + log color customization
│   └── RelayCommand.cs       # RelayCommand + AsyncRelayCommand
├── Views/
│   ├── WelcomePage.xaml/.cs
│   ├── FlowEditorPage.xaml/.cs
│   ├── TaskEditorDialog.xaml/.cs
│   ├── SettingsPage.xaml/.cs
│   ├── CompletionDialog.xaml/.cs  # Post-build summary dialog
│   └── DarkDialog.cs             # Dark-themed MessageBox replacements
├── Helpers/
│   ├── Logger.cs             # Debug-only file logger
│   ├── PathValidator.cs      # .uplugin validation, version extraction
│   └── FolderBrowser.cs      # COM IFileDialog wrapper (no WinForms dep)
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── App.xaml                  # Dark theme resources, global styles
├── App.xaml.cs
└── MainWindow.xaml/.cs       # Custom title bar, page navigation
```

## License

MIT
