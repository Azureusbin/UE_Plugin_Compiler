@echo off
REM Build UEPluginCompiler as a self-contained single-file exe

echo === Building UEPluginCompiler ===
echo.

dotnet publish UEPluginCompiler\UEPluginCompiler.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o .\publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo === Build successful! ===
    echo Output: .\publish\UEPluginCompiler.exe
) else (
    echo.
    echo === Build FAILED with exit code %ERRORLEVEL% ===
    exit /b %ERRORLEVEL%
)
