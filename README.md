# UE Plugin Compiler

A Windows GUI tool for batch-compiling UE plugins across multiple engine versions with configurable build flows.

## Features

- 🔍 **Auto-detects UE installations** — scans Windows registry + filesystem
- ✋ **Custom engine paths** — add source builds from any folder (Settings page)
- 📋 **BuildFlow system** — define multi-task pipelines, save/load `.uflow` files
- 🔀 **Batch compilation** — compile one plugin × N engines × M build modes
- 🔧 **Environment variables** — inject env vars per task (e.g. `EDITORCRYPT_BUILD_MODE`)
- 📊 **Real-time output** — live streaming UAT log with progress
- 📁 **Structured output** — results organized by `{TaskName}/{EngineVersion}/`
- 🎨 **Dark theme** — UE-inspired dark color scheme, custom title bar
- 💾 **Portable** — single `.exe`, all data in `Saved/` folder

## Requirements

- Windows 10 / 11 (64-bit)
- .NET 10 (self-contained exe available)
- At least one Unreal Engine installation

## Quick Start

1. Launch `UEPluginCompiler.exe`
2. Click **New BuildFlow** (or open a recent `.uflow`)
3. Click **+ Add Task** — pick engines, select `.uplugin`, set env vars
4. Repeat for each build mode
5. Set output directory → **Run All Tasks**

## Screens

| Screen | Purpose |
|--------|---------|
| **Welcome** | New / Open / Recent BuildFlows |
| **Flow Editor** | Task list, output dir, Run All, live log |
| **Task Editor** | Per-task engine selection, plugin, env vars |
| **Settings** | Engine management (⚙ in title bar) |

## BuildFlow Files (`.uflow`)

JSON format, stored wherever you choose:

```json
{
  "name": "EditorCrypt All Modes",
  "outputDir": "F:/Builds/EditorCrypt",
  "tasks": [
    {
      "name": "AllOn",
      "enginePaths": ["C:/Program Files/Epic Games/UE_5.7"],
      "pluginPath": "F:/.../EditorCrypt.uplugin",
      "envVars": { "EDITORCRYPT_BUILD_MODE": "AllOn" },
      "noP4": true
    }
  ]
}
```

- Open via Welcome screen or **File → Open** in Flow Editor
- Save via **Save / Save As**, auto-added to Recent list

## Directory Layout

```
UEPluginCompiler.exe
Saved/
├── settings.json          # Recent flows list
├── custom_engines.json    # User-added engine paths
├── debug.log              # Debug build only
└── logs/
    └── 2026-07-16_143052/
        ├── 5.7.log
        ├── 5.5.log
        └── _summary.log
```

## Build from Source

```bash
# Prerequisites: .NET 10 SDK
dotnet build

# Run (Debug)
dotnet run --project UEPluginCompiler

# Publish single-file exe
dotnet publish UEPluginCompiler -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true -o ./publish
# Or on Windows: publish.bat
```

## How It Works

### UE Detection
1. Registry: `HKLM\SOFTWARE\EpicGames\Unreal Engine` (64 + 32-bit views)
2. Filesystem: `C:\Program Files\Epic Games\UE_*`
3. Custom paths: added via Settings page, persisted to `Saved/custom_engines.json`

All entries validated against `RunUAT.bat` existence — stale registry entries are silently filtered.

### Compilation
1. For each Task → each selected Engine:
   - Write env vars into process
   - Compile to temp directory (avoids Chinese-path issues)
   - Copy results to `{OutputDir}/{TaskName}/{EngineVersion}/`
2. Live output streamed from `RunUAT.bat BuildPlugin`
3. On completion: summary with ✅/❌ per task per engine

## Project Structure

```
UEPluginCompiler/
├── Models/           # BuildFlow, BuildTask, CompileRequest, EngineInstall
├── Services/         # UEDetector, PluginCompiler, FlowSerializer, SettingsManager
├── ViewModels/       # WelcomeVM, FlowEditorVM, TaskEditorVM, SettingsVM, RelayCommand
├── Views/            # WelcomePage, FlowEditorPage, TaskEditorDialog, SettingsPage
├── Helpers/          # Logger, PathValidator, FolderBrowser (COM)
├── Converters/       # BoolToVisibility
└── App.xaml          # Dark theme resources + converters
```

## License

MIT
